using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebAPI.Services
{
    public class RsaKeyService
    {
        private readonly RSA _serverRsa;
        private readonly ConcurrentDictionary<string, string> _clientPublicKeys = new();

        public RsaKeyService()
        {
            _serverRsa = RSA.Create(2048);
        }

        public string GetServerPublicKeyBase64()
        {
            byte[] spki = _serverRsa.ExportSubjectPublicKeyInfo();
            return Convert.ToBase64String(spki);
        }

        public byte[] DecryptKeyBlock(byte[] encryptedKeyBlock)
        {
            return _serverRsa.Decrypt(encryptedKeyBlock, RSAEncryptionPadding.OaepSHA1);
        }

        public void SaveClientPublicKey(string clientId, string clientPublicKeyBase64)
        {
            _clientPublicKeys[clientId] = clientPublicKeyBase64;
        }

        public bool TryGetClientPublicKey(string clientId, out string? publicKeyBase64)
        {
            bool ok = _clientPublicKeys.TryGetValue(clientId, out string? value);
            publicKeyBase64 = value;
            return ok;
        }

        
        /// Mã hóa dữ liệu cho client sử dụng client public key
        
        public (string EncryptedKeyBlockBase64, string CipherDataBase64) EncryptForClient(string clientId, string plaintext)
        {
            if (!TryGetClientPublicKey(clientId, out string? publicKeyBase64) || string.IsNullOrWhiteSpace(publicKeyBase64))
                throw new InvalidOperationException($"Unknown clientId: {clientId}");

            // Normalize public key: loại bỏ whitespace
            string normalizedPublicKey = publicKeyBase64
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace(" ", "")
                .Replace("\t", "");

            // Build AES key and encrypt data
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                aes.GenerateIV();
                byte[] key = aes.Key;
                byte[] iv = aes.IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(key, iv);
                byte[] dataBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[] cipherBytes;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(dataBytes, 0, dataBytes.Length);
                    cs.FlushFinalBlock();
                    cipherBytes = ms.ToArray();
                }

                byte[] keyBlock = new byte[key.Length + iv.Length];
                Buffer.BlockCopy(key, 0, keyBlock, 0, key.Length);
                Buffer.BlockCopy(iv, 0, keyBlock, key.Length, iv.Length);

                using (var rsa = RSA.Create())
                {
                    try
                    {
                        // Import public key từ Base64 (SubjectPublicKeyInfo format)
                        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(normalizedPublicKey), out _);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Invalid public key format for clientId {clientId}: {ex.Message}");
                    }
                    byte[] encryptedKeyBlock = rsa.Encrypt(keyBlock, RSAEncryptionPadding.OaepSHA1);
                    return (
                        EncryptedKeyBlockBase64: Convert.ToBase64String(encryptedKeyBlock),
                        CipherDataBase64: Convert.ToBase64String(cipherBytes)
                    );
                }
            }
        }
    }
}


