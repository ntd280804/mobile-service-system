using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Services;
using WebAPI.Models;
using WebAPI.Helpers;
using WebAPI.Models.Security;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private readonly RsaKeyService _rsaKeyService;
        private readonly OracleConnectionManager _connManager;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public SecurityController(RsaKeyService rsaKeyService, OracleConnectionManager connManager, OracleSessionHelper oracleSessionHelper)
        {
            _rsaKeyService = rsaKeyService;
            _connManager = connManager;
            _oracleSessionHelper = oracleSessionHelper;
        }

        [HttpGet("server-public-key")] 
        public ActionResult<ApiResponse<string>> GetServerPublicKey()
        {
            return Ok(ApiResponse<string>.Ok(_rsaKeyService.GetServerPublicKeyBase64()));
        }

        [HttpPost("register-client-key")] 
        public ActionResult<ApiResponse<string>> RegisterClientKey([FromBody] RegisterClientKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return BadRequest(ApiResponse<string>.Fail("clientId is required."));

            string publicKeyToSave;

            // Nếu là admin (clientId bắt đầu bằng "admin-"), lấy public key từ DB
            if (request.ClientId.StartsWith("admin-", StringComparison.OrdinalIgnoreCase))
            {
                // Lấy username từ header Oracle session
                var username = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
                if (string.IsNullOrWhiteSpace(username))
                {
                    // Fallback: dùng public key từ request nếu không có session
                    if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                        return BadRequest(ApiResponse<string>.Fail("clientPublicKeyBase64 is required for admin without session."));
                    publicKeyToSave = request.ClientPublicKeyBase64;
                }
                else
                {
                    // Lấy public key từ DB
                    var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                    if (conn == null)
                    {
                        // Fallback: dùng public key từ request
                        if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                            return BadRequest(ApiResponse<string>.Fail("clientPublicKeyBase64 is required when session is invalid."));
                        publicKeyToSave = request.ClientPublicKeyBase64;
                    }
                    else
                    {
                        try
                        {
                            using var cmd = new OracleCommand("APP.GET_PUBLICKEY_BY_USERNAME", conn);
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                            var pPubKey = new OracleParameter("p_public_key", OracleDbType.Clob, ParameterDirection.Output);
                            cmd.Parameters.Add(pPubKey);
                            cmd.ExecuteNonQuery();

                            if (pPubKey.Value == DBNull.Value || pPubKey.Value == null)
                            {
                                // Không có public key trong DB, dùng từ request
                                if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                                    return BadRequest(ApiResponse<string>.Fail($"Public key not found in DB for username: {username}. Please provide clientPublicKeyBase64."));
                                publicKeyToSave = request.ClientPublicKeyBase64;
                            }
                            else
                            {
                                var clobPubKey = (Oracle.ManagedDataAccess.Types.OracleClob)pPubKey.Value;
                                publicKeyToSave = clobPubKey.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Fallback: dùng public key từ request
                            if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                                return BadRequest(ApiResponse<string>.Fail($"Error getting public key from DB: {ex.Message}. Please provide clientPublicKeyBase64."));
                            publicKeyToSave = request.ClientPublicKeyBase64;
                        }
                    }
                }
            }
            else
            {
                // Public client: dùng public key từ request
                if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                    return BadRequest(ApiResponse<string>.Fail("clientPublicKeyBase64 is required for public clients."));
                publicKeyToSave = request.ClientPublicKeyBase64;
            }

            _rsaKeyService.SaveClientPublicKey(request.ClientId, publicKeyToSave);
            return Ok(ApiResponse<string>.Ok("registered"));
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


