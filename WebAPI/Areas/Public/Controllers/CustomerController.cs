using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Models.Auth;
using WebAPI.Models.Security;
using WebAPI.Services;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ControllerHelper _helper;
        private const string CustomerClientId = "public-web";
        
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly EmailService _emailService;

        public CustomerController(
                                  ControllerHelper helper,
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper,
                                  EmailService emailService)
        {
            _helper = helper;
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
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
                var loginOutputs = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.LOGIN_CUSTOMER",
                    new[]
                    {
                        ("p_phone", OracleDbType.Varchar2, (object?)dto.Username ?? ""),
                        ("p_password", OracleDbType.Varchar2, (object?)dto.Password ?? "")
                    },
                    new[]
                    {
                        ("p_out_phone", OracleDbType.Varchar2),
                        ("p_out_result", OracleDbType.Varchar2)
                    });

                var result = loginOutputs["p_out_result"]?.ToString();
                var username = loginOutputs["p_out_phone"]?.ToString();
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
                var dto = SecurePayloadHelper.DecryptPayload<CustomerLoginDto>(rsaKeyService, payload);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Xử lý login
                var loginResult = Login(dto);
                var apiResp = ControllerResponseHelper.ExtractApiResponse(loginResult, "Sai số điện thoại hoặc mật khẩu.");

                return Ok(SecurePayloadHelper.EncryptResponse(rsaKeyService, CustomerClientId, apiResp));
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
                using var conn = _connManager.CreateDefaultConnection(); // dùng connection string từ Program.cs

                OracleHelper.ExecuteNonQuery(
                    conn,
                    "APP.REGISTER_CUSTOMER",
                    ("p_phone", OracleDbType.Varchar2, dto.Phone ?? ""),
                    ("p_full_name", OracleDbType.Varchar2, dto.FullName ?? ""),
                    ("p_password", OracleDbType.Varchar2, dto.Password ?? ""),
                    ("p_email", OracleDbType.Varchar2, dto.Email ?? ""),
                    ("p_address", OracleDbType.Varchar2, dto.Address ?? ""));

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
        public ActionResult<ApiResponse<string>> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.OldPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(ApiResponse<string>.Fail("Thiếu mật khẩu cũ hoặc mật khẩu mới."));

            var headerUsername = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
            if (string.IsNullOrWhiteSpace(headerUsername))
                return Unauthorized(ApiResponse<string>.Fail("Missing Oracle user header."));

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null)
            {
                var message = unauthorized is ObjectResult obj ? ControllerResponseHelper.ExtractMessage(obj.Value) : "Unauthorized";
                return Unauthorized(ApiResponse<string>.Fail(message ?? "Unauthorized"));
            }

                try
                {
                    OracleHelper.ExecuteNonQuery(
                        conn,
                        "APP.CHANGE_CUSTOMER_PASSWORD",
                        ("p_phone", OracleDbType.Varchar2, headerUsername),
                        ("p_old_password", OracleDbType.Varchar2, dto.OldPassword),
                        ("p_new_password", OracleDbType.Varchar2, dto.NewPassword));

                return Ok(ApiResponse<string>.Ok("Đổi mật khẩu thành công."));
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(ApiResponse<string>.Fail("Phiên Oracle đã bị kill. Vui lòng đăng nhập lại."));
            }
                catch (OracleException ex) when (ex.Number == 20001)
            {
                    return BadRequest(ApiResponse<string>.Fail("Mật khẩu cũ không đúng."));
            }
            catch (OracleException ex)
            {
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
                var dto = SecurePayloadHelper.DecryptPayload<ChangePasswordDto>(rsaKeyService, payload);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Xử lý change password
                var changePasswordResult = ChangePassword(dto);
                var apiResp = ControllerResponseHelper.ExtractApiResponse(changePasswordResult, "Change password failed");

                return Ok(SecurePayloadHelper.EncryptResponse(rsaKeyService, CustomerClientId, apiResp));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
            }
        }

        [HttpPost("forgot-password/generate-otp")]
        public async Task<IActionResult> GenerateOtpForForgotPassword([FromBody] ForgotPasswordGenerateOtpDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(ApiResponse<string>.Fail("Email không được để trống."));
            }

            try
            {
                using var conn = CreateDefaultConnectionWithRole("ROLE_VERIFY");
                var otpOutputs = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.GENERATE_OTP_CUSTOMER",
                    new[] { ("p_email", OracleDbType.Varchar2, (object?)dto.Email ?? "") },
                    new[]
                    {
                        ("p_otp", OracleDbType.Varchar2),
                        ("p_result", OracleDbType.Varchar2)
                    });

                string result = otpOutputs["p_result"]?.ToString() ?? "";
                string otp = otpOutputs["p_otp"]?.ToString() ?? "";

                if (result.Contains("Lỗi") || result.Contains("không tồn tại"))
                {
                    return BadRequest(ApiResponse<string>.Fail(result));
                }

                // Gửi OTP qua email
                bool emailSent = await _emailService.SendOtpEmailAsync(dto.Email, otp);
                
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
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || 
                string.IsNullOrWhiteSpace(dto.Otp) || string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập đầy đủ thông tin."));
            }

            try
            {
                using var conn = CreateDefaultConnectionWithRole("ROLE_VERIFY");
                var resetOutputs = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.RESET_PASSWORD_CUSTOMER",
                    new[]
                    {
                        ("p_email", OracleDbType.Varchar2, (object?)dto.Email ?? ""),
                        ("p_otp", OracleDbType.Varchar2, (object?)dto.Otp ?? ""),
                        ("p_new_password", OracleDbType.Varchar2, (object?)dto.NewPassword ?? "")
                    },
                    new[] { ("p_result", OracleDbType.Varchar2) });

                string result = resetOutputs["p_result"]?.ToString() ?? "";

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

        private OracleConnection CreateDefaultConnectionWithRole(string role)
        {
            var conn = _connManager.CreateDefaultConnection();
            using var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn);
            setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = role;
            setRoleCmd.ExecuteNonQuery();
            return conn;
        }
    }
}