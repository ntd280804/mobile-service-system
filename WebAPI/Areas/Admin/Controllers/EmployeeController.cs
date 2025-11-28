
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Linq;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Models.Auth;
using WebAPI.Services;
using WebAPI.Models.Security;
namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly ControllerHelper _helper;
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public EmployeeController(
            ControllerHelper helper,
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper)
        {
            _helper = helper;
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
        }

        // =====================
        // 🟢 GET ALL EMPLOYEES
        // =====================
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var result = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_ALL_EMPLOYEES",
                    "p_cursor",
                    reader => new
					{
						EmpId = reader.GetDecimal(0),
						FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
						Username = reader.IsDBNull(2) ? null : reader.GetString(2),
						Email = reader.IsDBNull(3) ? null : reader.GetString(3),
						Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
						Status = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Roles = reader.IsDBNull(6) ? "" : reader.GetString(6)
                    });

                return Ok(result);
            }, "Lỗi khi lấy danh sách nhân viên");
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
            if (conn == null) return Unauthorized(ApiResponse<string>.Fail("Unauthorized"));

            try
            {
                OracleHelper.ExecuteNonQuery(
                    conn,
                    "APP.CHANGE_EMPLOYEE_PASSWORD",
                    ("p_username", OracleDbType.Varchar2, headerUsername),
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
                
                // Lấy clientId từ session headers
                var username = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
                var platform = HttpContext.Request.Headers["X-Oracle-Platform"].ToString();
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(platform))
                    return StatusCode(500, ApiResponse<EncryptedPayload>.Fail("Cannot determine clientId from session headers"));
                
                // Sử dụng clientId với prefix "admin-" để lấy public key từ DB
                string clientId = "admin-" + username;
                
                // Đảm bảo public key đã được load từ DB vào RsaKeyService
                if (!rsaKeyService.TryGetClientPublicKey(clientId, out _))
                {
                    var connPubKey = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorizedPubKey);
                    if (connPubKey != null)
                    {
                        try
                        {
                            string? publicKeyFromDb = OracleHelper.ExecuteClobOutput(
                                connPubKey,
                                "APP.GET_PUBLICKEY_BY_USERNAME",
                                "p_public_key",
                                ("p_username", OracleDbType.Varchar2, username));

                            if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                                {
                                // Normalize public key
                                    string normalizedPublicKey = publicKeyFromDb
                                        .Replace("\r", "")
                                        .Replace("\n", "")
                                        .Replace(" ", "")
                                        .Replace("\t", "");
                                    
                                // Extract Base64 từ PEM format nếu có
                                    if (normalizedPublicKey.Contains("BEGIN") || normalizedPublicKey.Contains("END"))
                                    {
                                        int startIdx = normalizedPublicKey.IndexOf("-----BEGIN");
                                        int endIdx = normalizedPublicKey.IndexOf("-----END");
                                        if (startIdx >= 0 && endIdx > startIdx)
                                        {
                                            int beginEnd = normalizedPublicKey.IndexOf("-----", startIdx + 10);
                                            if (beginEnd > startIdx)
                                            {
                                                normalizedPublicKey = normalizedPublicKey.Substring(beginEnd + 5, endIdx - beginEnd - 5)
                                                    .Replace("\r", "")
                                                    .Replace("\n", "")
                                                    .Replace(" ", "");
                                            }
                                        }
                                    }
                                    
                                    rsaKeyService.SaveClientPublicKey(clientId, normalizedPublicKey);
                                }
                            }
                        catch
                        {
                            // Log error nhưng tiếp tục
                        }
                    }
                }
                
                var encryptedResponse = SecurePayloadHelper.EncryptResponse(rsaKeyService, clientId, apiResp);
                return Ok(encryptedResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
            }
        }
        // =====================
        // 🟢 GET EMPLOYEE BY ID
        // =====================
        [HttpGet("{id}")]
        [Authorize]
        public IActionResult Get(decimal id)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var rows = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_EMPLOYEE_BY_ID",
                    "p_cursor",
                    reader => new
					{
						EmpId = reader.GetDecimal(0),
						FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
						Username = reader.IsDBNull(2) ? null : reader.GetString(2),
						Email = reader.IsDBNull(3) ? null : reader.GetString(3),
						Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
						Status = reader.IsDBNull(5) ? null : reader.GetString(5)
                    },
                    ("p_empid", OracleDbType.Decimal, id));

                if (rows.Count == 0)
                return NotFound();

                return Ok(rows[0]);
            }, "Lỗi khi lấy thông tin nhân viên");
        }

        // =====================
        // 🔵 LOGIN
        // =====================
        [HttpPost("login")]
        public ActionResult<ApiResponse<EmployeeLoginResult>> Login([FromBody] EmployeeLoginDto dto)
        {
            if (dto == null) return BadRequest(ApiResponse<EmployeeLoginResult>.Fail("Dữ liệu không hợp lệ."));

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

                if (string.IsNullOrEmpty(hashedPwd))
                    throw new Exception("HASH_PASSWORD_20CHARS trả về null hoặc rỗng");
                // Tạo connection Oracle riêng cho session này
                conn = _connManager.CreateConnection(dto.Username, hashedPwd, platform, sessionId);
                string roles1;
                using (var cmd1 = new OracleCommand("APP.GET_EMPLOYEE_ROLES_BY_USERNAME", conn))
                {
                    cmd1.CommandType = CommandType.StoredProcedure; // function cũng dùng StoredProcedure

                    // Tham số RETURN
                    var returnParam = new OracleParameter("returnVal", OracleDbType.Varchar2, 50)
                    {
                        Direction = ParameterDirection.ReturnValue
                    };
                    cmd1.Parameters.Add(returnParam);

                    // Tham số input
                    cmd1.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                    cmd1.ExecuteNonQuery();

                    roles1 = returnParam.Value?.ToString();

                }// Set VPD context in this Oracle session
                string primaryRole = null;
                if (!string.IsNullOrWhiteSpace(roles1))
                {
                    var roleList = roles1.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                        .Select(r => r.ToUpperInvariant())
                                        .ToList();
                    if (roleList.Contains("ROLE_ADMIN")) primaryRole = "ROLE_ADMIN";
                    else if (roleList.Contains("ROLE_TIEPTAN")) primaryRole = "ROLE_TIEPTAN";
                    else if (roleList.Contains("ROLE_KITHUATVIEN")) primaryRole = "ROLE_KITHUATVIEN";
                    else if (roleList.Contains("ROLE_THUKHO")) primaryRole = "ROLE_THUKHO";
                }

                if (!string.IsNullOrEmpty(primaryRole))
                {
                    using (var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn))
                    {
                        setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = primaryRole;
                        setRoleCmd.ExecuteNonQuery();
                    }
                    decimal empId;
                    using (var setEmpCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_username(:p_username); END;", conn))
                    {
                        setEmpCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                        setEmpCmd.ExecuteNonQuery();
                    }
                    using (var getIdCmd = new OracleCommand("APP.GET_EMPLOYEE_ID_BY_USERNAME", conn))
                    {
                        getIdCmd.CommandType = CommandType.StoredProcedure;
                        getIdCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                        var outEmp = new OracleParameter("p_emp_id", OracleDbType.Decimal)
                        { Direction = ParameterDirection.Output };
                        getIdCmd.Parameters.Add(outEmp);
                        getIdCmd.ExecuteNonQuery();
                        empId = ((OracleDecimal)outEmp.Value).Value;
                    }
                    using (var setEmpCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_emp(:p_emp_id); END;", conn))
                    {
                        setEmpCmd.Parameters.Add("p_emp_id", OracleDbType.Decimal).Value = empId;
                        setEmpCmd.ExecuteNonQuery();
                    }
                }
                using var cmd = new OracleCommand("APP.LOGIN_EMPLOYEE", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;

                var outUsernameParam = new OracleParameter("p_out_username", OracleDbType.Varchar2, 100)
                { Direction = ParameterDirection.Output };
                var outRoleParam = new OracleParameter("p_out_role", OracleDbType.Varchar2, 50)
                { Direction = ParameterDirection.Output };
                var outResultParam = new OracleParameter("p_out_result", OracleDbType.Varchar2, 50)
                { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(outUsernameParam);
                cmd.Parameters.Add(outResultParam);
                cmd.Parameters.Add(outRoleParam);

                cmd.ExecuteNonQuery(); // chạy procedure trực tiếp

                var result = outResultParam.Value?.ToString();
                var username = outUsernameParam.Value?.ToString();
                var roles = outRoleParam.Value?.ToString();

                if (result != "SUCCESS")
                    return Unauthorized(ApiResponse<EmployeeLoginResult>.Fail("Đăng nhập thất bại."));

                // Tạo JWT token
                var token = _jwtHelper.GenerateToken(username, roles, sessionId);

                

                return Ok(ApiResponse<EmployeeLoginResult>.Ok(new EmployeeLoginResult
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
                    1017 => Unauthorized(ApiResponse<EmployeeLoginResult>.Fail("Sai username hoặc mật khẩu.")),
                    28000 => Unauthorized(ApiResponse<EmployeeLoginResult>.Fail("Tài khoản bị khóa.")),
                    28001 => Unauthorized(ApiResponse<EmployeeLoginResult>.Fail("Mật khẩu đã hết hạn.")),
                    _ => StatusCode(500, ApiResponse<EmployeeLoginResult>.Fail($"Oracle error {ex.Number}: {ex.Message}"))
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EmployeeLoginResult>.Fail(ex.Message));
            }
        }
        [HttpPost("login-secure-encrypted")]
        public ActionResult<ApiResponse<EncryptedPayload>> LoginSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                var dto = SecurePayloadHelper.DecryptPayload<EmployeeLoginDto>(rsaKeyService, payload);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Xử lý login
                var loginResult = Login(dto);
                var apiResp = ControllerResponseHelper.ExtractApiResponse(loginResult, "Đăng nhập thất bại.");

                // Mã hóa response (cả success và error đều được mã hóa)
                // Login dùng clientId = username + platform (RSA tự động generate, KHÔNG dùng "admin-")
                // Public key đã được register khi client gọi register-client-key trong InitializeAsync
                string clientId = dto.Username + dto.Platform;
                
                var encryptedResponse = SecurePayloadHelper.EncryptResponse(rsaKeyService, clientId, apiResp);
                return Ok(encryptedResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
            }
        }


        // =====================
        // 🔴 LOGOUT
        // =====================
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
            _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);

            return Ok(new { message = "Đăng xuất thành công." });
        }

        // =====================
        // 🟣 REGISTER EMPLOYEE
        // =====================
        [HttpPost("register")]
        [Authorize]
        public IActionResult Register([FromBody] EmployeeRegisterDto dto)
        {
            if (dto == null) return BadRequest("Invalid data");

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                string? privateKey = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.REGISTER_EMPLOYEE",
                    "p_private_key",
                    ("p_empid", OracleDbType.Decimal, DBNull.Value),
                    ("p_full_name", OracleDbType.Varchar2, dto.FullName ?? ""),
                    ("p_username", OracleDbType.Varchar2, dto.Username ?? ""),
                    ("p_password", OracleDbType.Varchar2, dto.Password ?? ""),
                    ("p_email", OracleDbType.Varchar2, dto.Email ?? ""),
                    ("p_phone", OracleDbType.Varchar2, dto.Phone ?? ""));

                if (string.IsNullOrEmpty(privateKey))
                    return Conflict(new { message = "Lỗi khi thêm nhân viên: không nhận được private key" });

                var fileBytes = Encoding.UTF8.GetBytes(privateKey);
                var fileName = $"{dto.Username}_private_key.txt";

                return File(fileBytes, "text/plain", fileName);
            }, "Lỗi khi thêm nhân viên");
        }

        [HttpPost("register-secure-encrypted")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> RegisterSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                var dto = SecurePayloadHelper.DecryptPayload<EmployeeRegisterDto>(rsaKeyService, payload);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Lấy clientId từ session headers
                var username = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
                var platform = HttpContext.Request.Headers["X-Oracle-Platform"].ToString();
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(platform))
                    return StatusCode(500, ApiResponse<EncryptedPayload>.Fail("Cannot determine clientId from session headers"));
                
                // Sử dụng clientId với prefix "admin-" để lấy public key từ DB
                string clientId = "admin-" + username;
                
                // Đảm bảo public key đã được load từ DB vào RsaKeyService
                if (!rsaKeyService.TryGetClientPublicKey(clientId, out _))
                {
                    var connPubKey = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorizedPubKey);
                    if (connPubKey != null)
                    {
                        try
                        {
                            string? publicKeyFromDb = OracleHelper.ExecuteClobOutput(
                                connPubKey,
                                "APP.GET_PUBLICKEY_BY_USERNAME",
                                "p_public_key",
                                ("p_username", OracleDbType.Varchar2, username));

                            if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                                {
                                // Normalize public key
                                    string normalizedPublicKey = publicKeyFromDb
                                        .Replace("\r", "")
                                        .Replace("\n", "")
                                        .Replace(" ", "")
                                        .Replace("\t", "");
                                    
                                // Extract Base64 từ PEM format nếu có
                                    if (normalizedPublicKey.Contains("BEGIN") || normalizedPublicKey.Contains("END"))
                                    {
                                        int startIdx = normalizedPublicKey.IndexOf("-----BEGIN");
                                        int endIdx = normalizedPublicKey.IndexOf("-----END");
                                        if (startIdx >= 0 && endIdx > startIdx)
                                        {
                                            int beginEnd = normalizedPublicKey.IndexOf("-----", startIdx + 10);
                                            if (beginEnd > startIdx)
                                            {
                                                normalizedPublicKey = normalizedPublicKey.Substring(beginEnd + 5, endIdx - beginEnd - 5)
                                                    .Replace("\r", "")
                                                    .Replace("\n", "")
                                                    .Replace(" ", "");
                                            }
                                        }
                                    }
                                    
                                    rsaKeyService.SaveClientPublicKey(clientId, normalizedPublicKey);
                                }
                            }
                        catch
                        {
                            // Log error nhưng tiếp tục
                        }
                    }
                }

                // Xử lý register
                try
                {
                    var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                    if (conn == null)
                    {
                        // Mã hóa error message
                        var encryptedError = SecurePayloadHelper.EncryptResponse(rsaKeyService, clientId, new { Success = false, Message = "Unauthorized" });
                        return Ok(encryptedError);
                    }

                    string? privateKey = OracleHelper.ExecuteClobOutput(
                        conn,
                        "APP.REGISTER_EMPLOYEE",
                        "p_private_key",
                        ("p_empid", OracleDbType.Decimal, DBNull.Value),
                        ("p_full_name", OracleDbType.Varchar2, dto.FullName ?? ""),
                        ("p_username", OracleDbType.Varchar2, dto.Username ?? ""),
                        ("p_password", OracleDbType.Varchar2, dto.Password ?? ""),
                        ("p_email", OracleDbType.Varchar2, dto.Email ?? ""),
                        ("p_phone", OracleDbType.Varchar2, dto.Phone ?? ""));

                    if (string.IsNullOrEmpty(privateKey))
                    {
                        var encryptedError = SecurePayloadHelper.EncryptResponse(
                            rsaKeyService,
                            clientId,
                            new { Success = false, Message = "Lỗi khi thêm nhân viên: không nhận được private key" });
                        return Ok(encryptedError);
                    }

                    // Tạo response object chứa private key base64 và filename
                    var responseObj = new
                    {
                        Success = true,
                        Type = "file",
                        PrivateKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(privateKey)),
                        FileName = $"{dto.Username}_private_key.txt"
                    };

                    var encryptedResponse = SecurePayloadHelper.EncryptResponse(rsaKeyService, clientId, responseObj);
                    return Ok(encryptedResponse);
                }
                catch (Exception ex)
                {
                    // Nếu có lỗi, mã hóa error message
                    var encryptedError = SecurePayloadHelper.EncryptResponse(
                        rsaKeyService,
                        clientId,
                        new { Success = false, Message = ex.Message });
                    return Ok(encryptedError);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
            }
        }

        [HttpPost("unlock")]
        [Authorize]
        public IActionResult UnlockUser([FromBody] UnlockEmployeeDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Username))
                return BadRequest(new { message = "Username không hợp lệ." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                OracleHelper.ExecuteNonQuery(
                    conn,
                    "APP.UNLOCK_DB_USER",
                    ("p_username", OracleDbType.Varchar2, dto.Username));

                return Ok(new { message = $"Tài khoản {dto.Username} đã được unlock." });
            }, "Lỗi khi unlock tài khoản");
        }

        

        [HttpPost("lock")]
        [Authorize]
        public IActionResult LockUser([FromBody] UnlockEmployeeDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Username))
                return BadRequest(new { message = "Username không hợp lệ." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                OracleHelper.ExecuteNonQuery(
                    conn,
                    "APP.LOCK_DB_USER",
                    ("p_username", OracleDbType.Varchar2, dto.Username));

                return Ok(new { message = $"Tài khoản {dto.Username} đã bị khóa." });
            }, "Lỗi khi khóa tài khoản");
        }

    }
}
