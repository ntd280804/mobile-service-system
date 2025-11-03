using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WebAPI.Services;
using WebAPI.Models;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private readonly RsaKeyService _rsaKeyService;

        public SecurityController(RsaKeyService rsaKeyService)
        {
            _rsaKeyService = rsaKeyService;
        }

        [HttpGet("server-public-key")] 
        public ActionResult<ApiResponse<string>> GetServerPublicKey()
        {
            return Ok(ApiResponse<string>.Ok(_rsaKeyService.GetServerPublicKeyBase64()));
        }

        public class RegisterClientKeyRequest
        {
            public string? ClientId { get; set; }
            public string? ClientPublicKeyBase64 { get; set; }
        }

        [HttpPost("register-client-key")] 
        public ActionResult<ApiResponse<string>> RegisterClientKey([FromBody] RegisterClientKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                return BadRequest(ApiResponse<string>.Fail("clientId and clientPublicKeyBase64 are required."));

            _rsaKeyService.SaveClientPublicKey(request.ClientId, request.ClientPublicKeyBase64);
            return Ok(ApiResponse<string>.Ok("registered"));
        }

        public class EncryptedPayload
        {
            public string? EncryptedKeyBlockBase64 { get; set; }
            public string? CipherDataBase64 { get; set; }
        }

        [HttpPost("decrypt-echo")] 
        public ActionResult<ApiResponse<string>> DecryptEcho([FromBody] EncryptedPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<string>.Fail("encryptedKeyBlockBase64 and cipherDataBase64 are required."));

            byte[] keyBlock = _rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));

            byte[] aesKey = new byte[32];
            byte[] iv = new byte[16];
            Buffer.BlockCopy(keyBlock, 0, aesKey, 0, 32);
            Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

            byte[] cipherBytes = Convert.FromBase64String(payload.CipherDataBase64);
            using (Aes aes = Aes.Create())
            {
                ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
                using (var ms = new MemoryStream(cipherBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                {
                    string plaintext = reader.ReadToEnd();
                    return Ok(ApiResponse<string>.Ok(plaintext));
                }
            }
        }

        public class EncryptForClientRequest
        {
            public string? ClientId { get; set; }
            public string? Plaintext { get; set; }
        }

        public class EncryptForClientResponse
        {
            public string? EncryptedKeyBlockBase64 { get; set; }
            public string? CipherDataBase64 { get; set; }
        }

        [HttpPost("encrypt-for-client")] 
        public ActionResult<ApiResponse<EncryptForClientResponse>> EncryptForClient([FromBody] EncryptForClientRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId) || request.Plaintext == null)
                return BadRequest(ApiResponse<EncryptForClientResponse>.Fail("clientId and plaintext are required."));

            if (!_rsaKeyService.TryGetClientPublicKey(request.ClientId, out string? publicKeyBase64) || string.IsNullOrWhiteSpace(publicKeyBase64))
                return NotFound(ApiResponse<EncryptForClientResponse>.Fail("Unknown clientId."));

            // Build AES key and encrypt data
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                aes.GenerateIV();
                byte[] key = aes.Key;
                byte[] iv = aes.IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(key, iv);
                byte[] dataBytes = Encoding.UTF8.GetBytes(request.Plaintext);
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
                    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                    byte[] encryptedKeyBlock = rsa.Encrypt(keyBlock, RSAEncryptionPadding.OaepSHA1);
                    return Ok(ApiResponse<EncryptForClientResponse>.Ok(new EncryptForClientResponse
                    {
                        EncryptedKeyBlockBase64 = Convert.ToBase64String(encryptedKeyBlock),
                        CipherDataBase64 = Convert.ToBase64String(cipherBytes)
                    }));
                }
            }
        }
    }
}


