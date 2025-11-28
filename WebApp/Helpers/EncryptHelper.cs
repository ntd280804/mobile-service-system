using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebApp.Helpers
{
    public static class EncryptHelper
    {
        // Encrypt data (input: plaintext, publicKeyBase64), output: (EncryptedKeyBlock, CipherData) đều dưới dạng base64
        public static (string EncryptedKeyBlock, string CipherData) HybridEncrypt(string plaintext, string publicKeyBase64)
        {
            // Tạo AES Key + IV
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                aes.GenerateIV();
                byte[] key = aes.Key;
                byte[] iv = aes.IV;

                // Encrypt dữ liệu bằng AES
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

                // Chuẩn bị KeyBlock: key + iv
                byte[] keyBlock = new byte[key.Length + iv.Length];
                Buffer.BlockCopy(key, 0, keyBlock, 0, key.Length);
                Buffer.BlockCopy(iv, 0, keyBlock, key.Length, iv.Length);

                // Mã hóa keyBlock bằng RSA public key
                using (var rsa = RSA.Create())
                {
                    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                    byte[] encryptedKeyBlock = rsa.Encrypt(keyBlock, RSAEncryptionPadding.OaepSHA1);
                    // Trả về
                    return (
                        EncryptedKeyBlock: Convert.ToBase64String(encryptedKeyBlock),
                        CipherData: Convert.ToBase64String(cipherBytes)
                    );
                }
            }
        }

        // Decrypt data (input: keyBlock base64, cipherData base64, privateKeyBase64)
        public static string HybridDecrypt(string encryptedKeyBlockBase64, string cipherDataBase64, string privateKeyBase64)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
                byte[] encryptedKeyBlock = Convert.FromBase64String(encryptedKeyBlockBase64);
                byte[] keyBlock = rsa.Decrypt(encryptedKeyBlock, RSAEncryptionPadding.OaepSHA1);

                // Tách key + iv (mặc định AES 256, block 16)
                byte[] key = new byte[32];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(keyBlock, 0, key, 0, 32);
                Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

                // Giải mã dữ liệu cipher
                byte[] cipherData = Convert.FromBase64String(cipherDataBase64);
                using (Aes aes = Aes.Create())
                {
                    ICryptoTransform decryptor = aes.CreateDecryptor(key, iv);
                    using (var ms = new MemoryStream(cipherData))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
    }
}































































