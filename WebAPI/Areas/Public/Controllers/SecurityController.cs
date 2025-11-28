using Microsoft.AspNetCore.Mvc;
using System;
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

            if (request.ClientId.StartsWith("admin-", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveAdminPublicKey(request, out var publicKey, out var errorResult))
                {
                    return errorResult!;
                    }

                _rsaKeyService.SaveClientPublicKey(request.ClientId, publicKey);
                return Ok(ApiResponse<string>.Ok("registered"));
            }

                if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                    return BadRequest(ApiResponse<string>.Fail("clientPublicKeyBase64 is required for public clients."));

            _rsaKeyService.SaveClientPublicKey(request.ClientId, request.ClientPublicKeyBase64);
            return Ok(ApiResponse<string>.Ok("registered"));
        }
        private bool TryResolveAdminPublicKey(RegisterClientKeyRequest request, out string publicKey, out ActionResult<ApiResponse<string>>? errorResult)
        {
            publicKey = string.Empty;
            errorResult = null;

            var username = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
            if (string.IsNullOrWhiteSpace(username))
            {
                if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                {
                    errorResult = BadRequest(ApiResponse<string>.Fail("clientPublicKeyBase64 is required for admin without session."));
                    return false;
                }

                publicKey = request.ClientPublicKeyBase64;
                return true;
            }

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null)
            {
                if (!string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                {
                    publicKey = request.ClientPublicKeyBase64;
                    return true;
                }

                errorResult = unauthorized is ObjectResult obj
                    ? StatusCode(obj.StatusCode ?? 401, ApiResponse<string>.Fail(ControllerResponseHelper.ExtractMessage(obj.Value) ?? "Unauthorized"))
                    : Unauthorized(ApiResponse<string>.Fail("Unauthorized"));

                return false;
            }

            try
            {
                var publicKeyFromDb = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.GET_PUBLICKEY_BY_USERNAME",
                    "p_public_key",
                    ("p_username", OracleDbType.Varchar2, username));

                if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                {
                    publicKey = publicKeyFromDb;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                {
                    publicKey = request.ClientPublicKeyBase64;
                    return true;
                }

                errorResult = BadRequest(ApiResponse<string>.Fail($"Public key not found in DB for username: {username}. Please provide clientPublicKeyBase64."));
                return false;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64))
                {
                    publicKey = request.ClientPublicKeyBase64;
                    return true;
                }

                errorResult = BadRequest(ApiResponse<string>.Fail($"Error getting public key from DB: {ex.Message}. Please provide clientPublicKeyBase64."));
                return false;
            }
        }
    }
}


