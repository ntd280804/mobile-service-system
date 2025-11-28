using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebAPI.Models;
using WebAPI.Services;
using WebAPI.Models.Security;
namespace WebAPI.Helpers
{
    
    /// Shared helpers for encrypting/decrypting payloads with RSA/AES hybrid scheme.
    
    public static class SecurePayloadHelper
    {
        public static T? DecryptPayload<T>(RsaKeyService rsaKeyService, EncryptedPayload payload)
        {
            byte[] keyBlock = rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));
            byte[] aesKey = new byte[32];
            byte[] iv = new byte[16];
            Buffer.BlockCopy(keyBlock, 0, aesKey, 0, 32);
            Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

            using var aes = Aes.Create();
            using var decryptor = aes.CreateDecryptor(aesKey, iv);
            byte[] cipherBytes = Convert.FromBase64String(payload.CipherDataBase64);
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            string plaintext = reader.ReadToEnd();

            return JsonSerializer.Deserialize<T>(plaintext);
        }

        public static ApiResponse<EncryptedPayload> EncryptResponse<TPayload>(
            RsaKeyService rsaKeyService,
            string clientId,
            TPayload payload)
        {
            string responseJson = JsonSerializer.Serialize(payload);
            var encryptedResponse = rsaKeyService.EncryptForClient(clientId, responseJson);

            return ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
            {
                EncryptedKeyBlockBase64 = encryptedResponse.EncryptedKeyBlockBase64,
                CipherDataBase64 = encryptedResponse.CipherDataBase64
            });
        }
    }
}

