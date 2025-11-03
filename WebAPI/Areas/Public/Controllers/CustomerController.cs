using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;

using WebAPI.Helpers;

using WebAPI.Services;
using WebAPI.Models.Auth;
using WebAPI.Models;
using System.Security.Cryptography;
using System.Text;
using System.IO;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public CustomerController(
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper)
        {
            
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
        }

        [HttpPost("login")]
        public ActionResult<ApiResponse<CustomerLoginResult>> Login([FromBody] CustomerLoginDto dto)
        {
            if (dto == null)
                return BadRequest(ApiResponse<CustomerLoginResult>.Fail("Invalid data"));

            try
            {
                string platform = dto.Platform;
                string sessionId = Guid.NewGuid().ToString();
                var conn = _connManager.CreateDefaultConnection();
                string hashedPwd;
                using (var cmd1 = new OracleCommand("HASH_PASSWORD_20CHARS", conn))
                {
                    cmd1.CommandType = CommandType.StoredProcedure; // function cũng dùng StoredProcedure

                    // Tham số RETURN
                    var returnParam = new OracleParameter("returnVal", OracleDbType.Varchar2, 50)
                    {
                        Direction = ParameterDirection.ReturnValue
                    };
                    cmd1.Parameters.Add(returnParam);

                    // Tham số input
                    cmd1.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;

                    cmd1.ExecuteNonQuery();

                    hashedPwd = returnParam.Value?.ToString();

                }
                conn = _connManager.CreateConnection(dto.Username, hashedPwd, platform, sessionId);

                using var cmd = new OracleCommand("APP.LOGIN_CUSTOMER", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Username;
                cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;

                var outPhoneParam = new OracleParameter("p_out_phone", OracleDbType.Varchar2, 100)
                { Direction = ParameterDirection.Output };
                var outResultParam = new OracleParameter("p_out_result", OracleDbType.Varchar2, 50)
                { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(outPhoneParam);
                cmd.Parameters.Add(outResultParam);

                cmd.ExecuteNonQuery(); // chạy procedure trực tiếp

                var result = outResultParam.Value?.ToString();
                var username = outPhoneParam.Value?.ToString();
                var roles = "ROLE_KHACHHANG";

                if (result != "SUCCESS")
                    return Unauthorized(ApiResponse<CustomerLoginResult>.Fail("Sai số điện thoại hoặc mật khẩu."));

                var token = _jwtHelper.GenerateToken(username, roles, sessionId);
                return Ok(ApiResponse<CustomerLoginResult>.Ok(new CustomerLoginResult
                {
                    Username = username,
                    Roles = roles,
                    Result = result,
                    SessionId = sessionId,
                    Token = token
                }));
            }
            catch (OracleException ex)
            {
                return ex.Number switch
                {
                    1017 => Unauthorized(new { result = "FAILED", message = "Sai số điện thoại hoặc mật khẩu." }),
                    28000 => Unauthorized(new { result = "LOCKED", message = "Tài khoản bị khóa (profile)." }),
                    28001 => Unauthorized(new { result = "EXPIRED", message = "Mật khẩu đã hết hạn (profile)." }),
                    _ => StatusCode(500, new { result = "FAILED", message = $"Oracle error {ex.Number}: {ex.Message}" })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<CustomerLoginResult>.Fail(ex.Message));
            }
        }


        [HttpPost("login-secure")]
        public ActionResult<ApiResponse<CustomerLoginResult>> LoginSecure([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<CustomerLoginResult>.Fail("Invalid encrypted payload"));

            try
            {
                byte[] keyBlock = rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));
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
                        var dto = System.Text.Json.JsonSerializer.Deserialize<CustomerLoginDto>(plaintext);
                        if (dto == null)
                            return BadRequest(ApiResponse<CustomerLoginResult>.Fail("Cannot parse payload"));
                        return Login(dto);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<CustomerLoginResult>.Fail(ex.Message));
            }
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] CustomerRegisterDto dto)
        {
            if (dto == null) return BadRequest("Invalid data");

            try
            {
                // Tạo connection admin hoặc user có quyền gọi procedure
                var conn = _connManager.CreateDefaultConnection(); // dùng connection string từ Program.cs


                using var cmd = new OracleCommand("APP.REGISTER_CUSTOMER", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;
                cmd.Parameters.Add("p_full_name", OracleDbType.Varchar2).Value = dto.FullName;
                cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email;
                cmd.Parameters.Add("p_address", OracleDbType.Varchar2).Value = dto.Address;

                cmd.ExecuteNonQuery(); // chạy procedure trực tiếp

                return Ok(new { message = "Customer registered" });
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = $"Oracle error {ex.Number}: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Conflict(new { message = "Register failed", detail = ex.Message });
            }
        }


        // POST: api/Admin/Customer/logout
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
            _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);

            return Ok(new { message = "Đăng xuất thành công." });

        }

        // DTO
    }
}