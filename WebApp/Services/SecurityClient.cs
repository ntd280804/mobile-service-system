using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using WebApp.Helpers;

namespace WebApp.Services
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    public class SecurityClient
    {
        private readonly HttpClient _httpClient;
        private string? _clientId;
        private string? _clientPrivateKeyBase64;
        private string? _serverPublicKeyBase64;

        public SecurityClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Set headers cho authenticated requests (JWT token và Oracle session headers)
        /// </summary>
        public void SetHeaders(string jwtToken, string username, string platform, string sessionId)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");
            
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);
        }

        public async Task InitializeAsync(string baseApiUrl, string clientId, string? existingPrivateKeyPem = null)
        {
            _clientId = clientId;
            _httpClient.BaseAddress = new Uri(baseApiUrl);
            
            // 1) Get server public key
            var keyResp = await _httpClient.GetFromJsonAsync<ApiResponse<string>>("api/public/security/server-public-key");
            if (keyResp == null || !keyResp.Success || string.IsNullOrWhiteSpace(keyResp.Data))
                throw new InvalidOperationException(keyResp?.Error ?? "Cannot get server public key");
            _serverPublicKeyBase64 = keyResp.Data;

            // 2) Sử dụng private key có sẵn hoặc generate mới
            if (!string.IsNullOrWhiteSpace(existingPrivateKeyPem))
            {
                // Dùng private key đã upload
                try
                {
                    using (var rsa = RSA.Create())
                    {
                        rsa.ImportFromPem(existingPrivateKeyPem);
                        string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
                        _clientPrivateKeyBase64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

                        var reg = await _httpClient.PostAsJsonAsync("api/public/security/register-client-key", new
                        {
                            clientId = _clientId,
                            clientPublicKeyBase64 = publicKeyBase64
                        });
                        if (!reg.IsSuccessStatusCode)
                        {
                            var content = await reg.Content.ReadAsStringAsync();
                            throw new InvalidOperationException($"Register client key failed: {(int)reg.StatusCode} {reg.ReasonPhrase} - {content}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Không thể sử dụng private key đã upload: {ex.Message}");
                }
            }
            else
            {
                // Generate client RSA key pair mới
                using (var rsa = RSA.Create(2048))
                {
                    string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
                    _clientPrivateKeyBase64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

                    var reg = await _httpClient.PostAsJsonAsync("api/public/security/register-client-key", new
                    {
                        clientId = _clientId,
                        clientPublicKeyBase64 = publicKeyBase64
                    });
                    if (!reg.IsSuccessStatusCode)
                    {
                        var content = await reg.Content.ReadAsStringAsync();
                        throw new InvalidOperationException($"Register client key failed: {(int)reg.StatusCode} {reg.ReasonPhrase} - {content}");
                    }
                }
            }
        }

        /// <summary>
        /// Lấy private key PEM để lưu vào session
        /// </summary>
        public string? GetPrivateKeyPem()
        {
            if (string.IsNullOrWhiteSpace(_clientPrivateKeyBase64))
                return null;

            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(_clientPrivateKeyBase64), out _);
                    return rsa.ExportPkcs8PrivateKeyPem();
                }
            }
            catch
            {
                return null;
            }
        }
        // Generic: send any request encrypted, expect ApiResponse<TRes>
        public async Task<TRes> PostEncryptedAsync<TReq, TRes>(string path, TReq request)
        {
            if (_serverPublicKeyBase64 == null)
                throw new InvalidOperationException("SecurityClient is not initialized.");

            string json = JsonSerializer.Serialize(request);
            var encrypted = EncryptHelper.HybridEncrypt(json, _serverPublicKeyBase64);
            var res = await _httpClient.PostAsJsonAsync(path, new
            {
                encryptedKeyBlockBase64 = encrypted.EncryptedKeyBlock,
                cipherDataBase64 = encrypted.CipherData
            });
            var api = await res.Content.ReadFromJsonAsync<ApiResponse<TRes>>();
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                throw new InvalidOperationException(api?.Error ?? content ?? $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}");
            }
            if (api == null || !api.Success || api.Data == null)
                throw new InvalidOperationException(api?.Error ?? "Invalid response");
            return api.Data;
        }

        // Generic: send encrypted request, receive encrypted response, decrypt and return TRes
        public async Task<TRes> PostEncryptedAndGetEncryptedAsync<TReq, TRes>(string path, TReq request)
        {
            if (_serverPublicKeyBase64 == null || _clientPrivateKeyBase64 == null)
                throw new InvalidOperationException("SecurityClient is not initialized.");

            // 1. Mã hóa request
            string json = JsonSerializer.Serialize(request);
            var encrypted = EncryptHelper.HybridEncrypt(json, _serverPublicKeyBase64);

            // 2. Gửi request
            var res = await _httpClient.PostAsJsonAsync(path, new
            {
                encryptedKeyBlockBase64 = encrypted.EncryptedKeyBlock,
                cipherDataBase64 = encrypted.CipherData
            });

            // 3. Kiểm tra status code trước
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                // Nếu response rỗng, trả về thông báo lỗi từ status code
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}");
                }
                // Thử deserialize nếu có content
                try
                {
                    var errorApi = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<Envelope>>(content);
                    throw new InvalidOperationException(errorApi?.Error ?? content ?? $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}");
                }
                catch
                {
                    throw new InvalidOperationException(content ?? $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}");
                }
            }

            // 4. Nhận encrypted response
            var api = await res.Content.ReadFromJsonAsync<ApiResponse<Envelope>>();
            if (api == null)
            {
                // Thử đọc raw content để debug
                var rawContent = await res.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Cannot deserialize response. Raw content: {rawContent?.Substring(0, Math.Min(200, rawContent?.Length ?? 0))}");
            }
            
            if (!api.Success)
                throw new InvalidOperationException(api.Error ?? "Request failed");
            
            if (api.Data == null)
                throw new InvalidOperationException("Response envelope data is null");

            // 6. Giải mã response
            string decryptedJson = EncryptHelper.HybridDecrypt(
                api.Data.EncryptedKeyBlockBase64!, 
                api.Data.CipherDataBase64!, 
                _clientPrivateKeyBase64);

            // 7. Deserialize thành ApiResponse<TRes> (vì server trả về ApiResponse<TRes> đã được mã hóa)
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<TRes>>(decryptedJson);
            if (apiResponse == null)
            {
                throw new InvalidOperationException($"Cannot deserialize decrypted response. Decrypted JSON: {decryptedJson?.Substring(0, Math.Min(500, decryptedJson?.Length ?? 0))}");
            }
            
            if (!apiResponse.Success)
                throw new InvalidOperationException(apiResponse.Error ?? "Request failed");
            
            if (apiResponse.Data == null)
            {
                // Có thể response không phải là ApiResponse<TRes> mà là TRes trực tiếp
                // Thử deserialize trực tiếp thành TRes
                try
                {
                    var directData = JsonSerializer.Deserialize<TRes>(decryptedJson);
                    if (directData != null)
                        return directData;
                }
                catch
                {
                    // Ignore, sẽ throw error bên dưới
                }
                throw new InvalidOperationException($"Response data is null. Decrypted JSON: {decryptedJson?.Substring(0, Math.Min(500, decryptedJson?.Length ?? 0))}");
            }
            
            return apiResponse.Data;
        }

        public class Envelope
        {
            public string? EncryptedKeyBlockBase64 { get; set; }
            public string? CipherDataBase64 { get; set; }
        }
    }
}


