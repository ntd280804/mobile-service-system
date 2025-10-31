using System;
using System.Net.Http;
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

        public async Task InitializeAsync(string baseApiUrl, string clientId)
        {
            _clientId = clientId;
            _httpClient.BaseAddress = new Uri(baseApiUrl);

            // 1) Get server public key
            var keyResp = await _httpClient.GetFromJsonAsync<ApiResponse<string>>("api/public/security/server-public-key");
            if (keyResp == null || !keyResp.Success || string.IsNullOrWhiteSpace(keyResp.Data))
                throw new InvalidOperationException(keyResp?.Error ?? "Cannot get server public key");
            _serverPublicKeyBase64 = keyResp.Data;

            // 2) Generate client RSA key pair and register public key
            using (var rsa = RSA.Create(2048))
            {
                string publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
                _clientPrivateKeyBase64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

                var reg = await _httpClient.PostAsJsonAsync("api/public/security/register-client-key", new
                {
                    clientId = _clientId,
                    clientPublicKeyBase64 = publicKeyBase64
                });
                reg.EnsureSuccessStatusCode();
            }
        }

        public async Task<string> PostEncryptedEchoAsync(object payload)
        {
            if (_serverPublicKeyBase64 == null)
                throw new InvalidOperationException("SecurityClient is not initialized.");

            string plaintext = JsonSerializer.Serialize(payload);
            var encrypted = EncryptHelper.HybridEncrypt(plaintext, _serverPublicKeyBase64);

            var response = await _httpClient.PostAsJsonAsync("api/public/security/decrypt-echo", new
            {
                encryptedKeyBlockBase64 = encrypted.EncryptedKeyBlock,
                cipherDataBase64 = encrypted.CipherData
            });

            response.EnsureSuccessStatusCode();
            var api = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
            if (api == null || !api.Success || api.Data == null)
                throw new InvalidOperationException(api?.Error ?? "Invalid response");
            return api.Data;
        }

        public async Task<string> GetEncryptedFromServerAsync(string message)
        {
            if (_clientPrivateKeyBase64 == null)
                throw new InvalidOperationException("SecurityClient is not initialized.");

            var res = await _httpClient.PostAsJsonAsync("api/public/security/encrypt-for-client", new
            {
                clientId = _clientId,
                plaintext = message
            });
            res.EnsureSuccessStatusCode();
            var api = await res.Content.ReadFromJsonAsync<ApiResponse<Envelope>>();
            if (api == null || !api.Success || api.Data == null)
                throw new InvalidOperationException("Empty response envelope");

            string plaintext = EncryptHelper.HybridDecrypt(api.Data.EncryptedKeyBlockBase64!, api.Data.CipherDataBase64!, _clientPrivateKeyBase64);
            return plaintext;
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
            res.EnsureSuccessStatusCode();
            var api = await res.Content.ReadFromJsonAsync<ApiResponse<TRes>>();
            if (api == null || !api.Success || api.Data == null)
                throw new InvalidOperationException(api?.Error ?? "Invalid response");
            return api.Data;
        }

        public class Envelope
        {
            public string? EncryptedKeyBlockBase64 { get; set; }
            public string? CipherDataBase64 { get; set; }
        }
    }
}


