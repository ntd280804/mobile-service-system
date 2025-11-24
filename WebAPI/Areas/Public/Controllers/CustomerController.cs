using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Models.Security;
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
        private readonly CustomerQrLoginService _customerQrLoginService;
        private readonly EmailService _emailService;

        public CustomerController(
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper,
                                  CustomerQrLoginService customerQrLoginService,
                                  EmailService emailService)
        {
            
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
            _customerQrLoginService = customerQrLoginService;
            _emailService = emailService;
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
                using (var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn))
                {
                    setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = "ROLE_KHACHHANG";
                    setRoleCmd.ExecuteNonQuery();
                }
                using (var setCusCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_customer(:p_phone); END;", conn))
                {
                    setCusCmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Username;
                    setCusCmd.ExecuteNonQuery();
                }
                using var cmd = new OracleCommand("APP.LOGIN_CUSTOMER", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true; // ensure parameters match by name (ODP.NET default is positional)

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

                // Set VPD context in this Oracle session for customer
                
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

        [HttpPost("login-secure-encrypted")]
        public ActionResult<ApiResponse<EncryptedPayload>> LoginSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                // Giải mã request từ client
                byte[] keyBlock = rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));
                byte[] aesKey = new byte[32];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(keyBlock, 0, aesKey, 0, 32);
                Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

                byte[] cipherBytes = Convert.FromBase64String(payload.CipherDataBase64);
                string plaintext;
                using (Aes aes = Aes.Create())
                {
                    ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        plaintext = reader.ReadToEnd();
                    }
                }

                var dto = System.Text.Json.JsonSerializer.Deserialize<CustomerLoginDto>(plaintext);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Xử lý login
                var loginResult = Login(dto);
                ApiResponse<CustomerLoginResult> apiResp;
                
                if (loginResult.Result is ObjectResult objResult && objResult.Value is ApiResponse<CustomerLoginResult> resp)
                {
                    apiResp = resp;
                }
                else if (loginResult.Result is UnauthorizedObjectResult unauthorizedObj)
                {
                    // Xử lý UnauthorizedObjectResult có thể chứa ApiResponse hoặc anonymous object
                    if (unauthorizedObj.Value is ApiResponse<CustomerLoginResult> unauthResp)
                    {
                        apiResp = unauthResp;
                    }
                    else if (unauthorizedObj.Value != null)
                    {
                        // Thử deserialize anonymous object để lấy message
                        try
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(unauthorizedObj.Value);
                            var anonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                            string message = "Unauthorized";
                            if (anonObj.TryGetProperty("message", out var msgProp))
                                message = msgProp.GetString() ?? message;
                            else if (anonObj.TryGetProperty("Message", out var msgProp2))
                                message = msgProp2.GetString() ?? message;
                            apiResp = ApiResponse<CustomerLoginResult>.Fail(message);
                        }
                        catch
                        {
                            apiResp = ApiResponse<CustomerLoginResult>.Fail("Sai số điện thoại hoặc mật khẩu.");
                        }
                    }
                    else
                    {
                        apiResp = ApiResponse<CustomerLoginResult>.Fail("Sai số điện thoại hoặc mật khẩu.");
                    }
                }
                else if (loginResult.Result is StatusCodeResult statusCodeResult)
                {
                    string errorMsg = "Login failed";
                    if (statusCodeResult.StatusCode == 401)
                        errorMsg = "Sai số điện thoại hoặc mật khẩu.";
                    apiResp = ApiResponse<CustomerLoginResult>.Fail(errorMsg);
                }
                else
                {
                    apiResp = ApiResponse<CustomerLoginResult>.Fail("Unexpected response format");
                }

                // Mã hóa response (cả success và error đều được mã hóa)
                // Customer dùng clientId là "public-web" (fixed)
                string responseJson = System.Text.Json.JsonSerializer.Serialize(apiResp);
                string clientId = "public-web";
                var encryptedResponse = rsaKeyService.EncryptForClient(clientId, responseJson);
                return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                {
                    EncryptedKeyBlockBase64 = encryptedResponse.EncryptedKeyBlockBase64,
                    CipherDataBase64 = encryptedResponse.CipherDataBase64
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
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

        [HttpPost("change-password")]
        [Authorize]
        public ActionResult<ApiResponse<string>> ChangePassword([FromBody] changePasswordDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.OldPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(ApiResponse<string>.Fail("Thiếu mật khẩu cũ hoặc mật khẩu mới."));

            var headerUsername = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
            if (string.IsNullOrWhiteSpace(headerUsername))
                return Unauthorized(ApiResponse<string>.Fail("Missing Oracle user header."));

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return Unauthorized(ApiResponse<string>.Fail("Unauthorized"));

            try
            {
                using var cmd = new OracleCommand("APP.CHANGE_CUSTOMER_PASSWORD", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;
                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2, ParameterDirection.Input).Value = headerUsername;
                cmd.Parameters.Add("p_old_password", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.OldPassword;
                cmd.Parameters.Add("p_new_password", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.NewPassword;
                cmd.ExecuteNonQuery();
                return Ok(ApiResponse<string>.Ok("Đổi mật khẩu thành công."));
            }
            catch (OracleException ex)
            {
                if (ex.Number == 20001)
                    return BadRequest(ApiResponse<string>.Fail("Mật khẩu cũ không đúng."));
                return StatusCode(500, ApiResponse<string>.Fail($"Oracle error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(ex.Message));
            }
        }
        [HttpPost("change-password-secure-encrypted")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> ChangePasswordSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                // Giải mã request từ client
                byte[] keyBlock = rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));
                byte[] aesKey = new byte[32];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(keyBlock, 0, aesKey, 0, 32);
                Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

                byte[] cipherBytes = Convert.FromBase64String(payload.CipherDataBase64);
                string plaintext;
                using (Aes aes = Aes.Create())
                {
                    ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        plaintext = reader.ReadToEnd();
                    }
                }

                var dto = System.Text.Json.JsonSerializer.Deserialize<changePasswordDto>(plaintext);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Xử lý change password
                var changePasswordResult = ChangePassword(dto);
                ApiResponse<string> apiResp;
                
                if (changePasswordResult.Result is ObjectResult objResult && objResult.Value is ApiResponse<string> resp)
                {
                    apiResp = resp;
                }
                else if (changePasswordResult.Result is BadRequestObjectResult badRequestObj && badRequestObj.Value is ApiResponse<string> badResp)
                {
                    // Lấy error message từ BadRequestObjectResult
                    apiResp = badResp;
                }
                else if (changePasswordResult.Result is UnauthorizedObjectResult unauthorizedObj && unauthorizedObj.Value is ApiResponse<string> unauthResp)
                {
                    // Lấy error message từ UnauthorizedObjectResult
                    apiResp = unauthResp;
                }
                else if (changePasswordResult.Result is StatusCodeResult statusCodeResult)
                {
                    string errorMsg = "Change password failed";
                    if (statusCodeResult.StatusCode == 400)
                        errorMsg = "Bad request";
                    else if (statusCodeResult.StatusCode == 401)
                        errorMsg = "Unauthorized";
                    apiResp = ApiResponse<string>.Fail(errorMsg);
                }
                else
                {
                    apiResp = ApiResponse<string>.Fail("Unexpected response format");
                }

                // Mã hóa response
                string responseJson = System.Text.Json.JsonSerializer.Serialize(apiResp);
                
                // Customer dùng clientId là "public-web" (fixed)
                string clientId = "public-web";
                var encryptedResponse = rsaKeyService.EncryptForClient(clientId, responseJson);
                return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                {
                    EncryptedKeyBlockBase64 = encryptedResponse.EncryptedKeyBlockBase64,
                    CipherDataBase64 = encryptedResponse.CipherDataBase64
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
            }
        }

        [HttpPost("forgot-password/generate-otp")]
        public async Task<IActionResult> GenerateOtpForForgotPassword([FromBody] ForgotPasswordGenerateOtpDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Phone))
            {
                return BadRequest(ApiResponse<string>.Fail("Số điện thoại không được để trống."));
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(ApiResponse<string>.Fail("Email không được để trống."));
            }

            try
            {
                var conn = _connManager.CreateDefaultConnection();
                using var cmd = new OracleCommand("APP.GENERATE_OTP_CUSTOMER", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;
                var pOtp = new OracleParameter("p_otp", OracleDbType.Varchar2, 10, null, ParameterDirection.Output);
                var pResult = new OracleParameter("p_result", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                cmd.Parameters.Add(pOtp);
                cmd.Parameters.Add(pResult);

                cmd.ExecuteNonQuery();

                string result = pResult.Value?.ToString() ?? "";
                string otp = pOtp.Value?.ToString() ?? "";

                if (result.Contains("Lỗi") || result.Contains("không tồn tại"))
                {
                    return BadRequest(ApiResponse<string>.Fail(result));
                }

                // Gửi OTP qua email
                bool emailSent = await _emailService.SendOtpEmailAsync(dto.Email, otp, dto.Phone);
                
                if (!emailSent)
                {
                    return StatusCode(500, ApiResponse<string>.Fail("Không thể gửi email OTP. Vui lòng thử lại sau."));
                }

                // Không trả về OTP trong response (bảo mật)
                return Ok(ApiResponse<ForgotPasswordOtpResponse>.Ok(new ForgotPasswordOtpResponse
                {
                    Otp = "", // Không trả về OTP
                    Message = "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư."
                }));
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                return Unauthorized(ApiResponse<string>.Fail("Phiên Oracle đã bị kill. Vui lòng thử lại."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi tạo OTP: " + ex.Message));
            }
        }

        [HttpPost("forgot-password/reset")]
        public IActionResult ResetPasswordWithOtp([FromBody] ForgotPasswordResetDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Phone) || 
                string.IsNullOrWhiteSpace(dto.Otp) || string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập đầy đủ thông tin."));
            }

            try
            {
                var conn = _connManager.CreateDefaultConnection();
                using var cmd = new OracleCommand("APP.RESET_PASSWORD_CUSTOMER", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;
                cmd.Parameters.Add("p_otp", OracleDbType.Varchar2).Value = dto.Otp;
                cmd.Parameters.Add("p_new_password", OracleDbType.Varchar2).Value = dto.NewPassword;
                var pResult = new OracleParameter("p_result", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                cmd.Parameters.Add(pResult);

                cmd.ExecuteNonQuery();

                string result = pResult.Value?.ToString() ?? "";

                if (result.Contains("Lỗi") || result.Contains("không hợp lệ") || result.Contains("không tồn tại"))
                {
                    return BadRequest(ApiResponse<string>.Fail(result));
                }

                return Ok(ApiResponse<string>.Ok(result));
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                return Unauthorized(ApiResponse<string>.Fail("Phiên Oracle đã bị kill. Vui lòng thử lại."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi đặt lại mật khẩu: " + ex.Message));
            }
        }

        // DTO
        public class ForgotPasswordGenerateOtpDto
        {
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        public class ForgotPasswordResetDto
        {
            public string Phone { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        public class ForgotPasswordOtpResponse
        {
            public string Otp { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }
    }
}