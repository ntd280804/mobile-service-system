
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public EmployeeController(
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper)
        {
            
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
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_EMPLOYEES", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
					list.Add(new
					{
						EmpId = reader.GetDecimal(0),
						FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
						Username = reader.IsDBNull(2) ? null : reader.GetString(2),
						Email = reader.IsDBNull(3) ? null : reader.GetString(3),
						Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
						Status = reader.IsDBNull(5) ? null : reader.GetString(5),
						Roles = reader.IsDBNull(6) ? "" : reader.GetString(6) // Cột Roles
					});
                }

                return Ok(list);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                // Retrieve session info from HttpContext
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách nhân viên", detail = ex.Message });
            }
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
                using var cmd = new OracleCommand("APP.CHANGE_EMPLOYEE_PASSWORD", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = headerUsername;
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
                    // Lấy public key từ DB và lưu vào RsaKeyService
                    var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                    if (conn != null)
                    {
                        try
                        {
                            using var cmd = new OracleCommand("APP.GET_PUBLICKEY_BY_USERNAME", conn);
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                            var pPubKey = new OracleParameter("p_public_key", OracleDbType.Clob, ParameterDirection.Output);
                            cmd.Parameters.Add(pPubKey);
                            cmd.ExecuteNonQuery();

                            if (pPubKey.Value != DBNull.Value && pPubKey.Value != null)
                            {
                                var clobPubKey = (Oracle.ManagedDataAccess.Types.OracleClob)pPubKey.Value;
                                string publicKeyFromDb = clobPubKey.Value?.Trim() ?? "";
                                if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                                {
                                    // Normalize public key: loại bỏ whitespace và kiểm tra format
                                    string normalizedPublicKey = publicKeyFromDb
                                        .Replace("\r", "")
                                        .Replace("\n", "")
                                        .Replace(" ", "")
                                        .Replace("\t", "");
                                    
                                    // Nếu có PEM headers, extract Base64
                                    if (normalizedPublicKey.Contains("BEGIN") || normalizedPublicKey.Contains("END"))
                                    {
                                        // Extract Base64 từ PEM format
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
                        }
                        catch (Exception ex)
                        {
                            // Log error nhưng tiếp tục, sẽ throw error sau nếu không có public key
                        }
                    }
                }
                
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
        // =====================
        // 🟢 GET EMPLOYEE BY ID
        // =====================
        [HttpGet("{id}")]
        [Authorize]
        public IActionResult Get(decimal id)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_EMPLOYEE_BY_ID", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_empid", OracleDbType.Decimal).Value = id;
                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();

				if (reader.Read())
				{
					return Ok(new
					{
						EmpId = reader.GetDecimal(0),
						FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
						Username = reader.IsDBNull(2) ? null : reader.GetString(2),
						Email = reader.IsDBNull(3) ? null : reader.GetString(3),
						Phone = reader.IsDBNull(4) ? null : reader.GetString(4),
						Status = reader.IsDBNull(5) ? null : reader.GetString(5)
					});
				}

                return NotFound();
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin nhân viên", detail = ex.Message });
            }
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

                var dto = System.Text.Json.JsonSerializer.Deserialize<EmployeeLoginDto>(plaintext);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Xử lý login
                var loginResult = Login(dto);
                ApiResponse<EmployeeLoginResult> apiResp;
                
                if (loginResult.Result is ObjectResult objResult && objResult.Value is ApiResponse<EmployeeLoginResult> resp)
                {
                    apiResp = resp;
                }
                else if (loginResult.Result is UnauthorizedObjectResult unauthorizedObj)
                {
                    // Xử lý UnauthorizedObjectResult có thể chứa ApiResponse hoặc anonymous object
                    if (unauthorizedObj.Value is ApiResponse<EmployeeLoginResult> unauthResp)
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
                            apiResp = ApiResponse<EmployeeLoginResult>.Fail(message);
                        }
                        catch
                        {
                            apiResp = ApiResponse<EmployeeLoginResult>.Fail("Đăng nhập thất bại.");
                        }
                    }
                    else
                    {
                        apiResp = ApiResponse<EmployeeLoginResult>.Fail("Đăng nhập thất bại.");
                    }
                }
                else if (loginResult.Result is StatusCodeResult statusCodeResult)
                {
                    string errorMsg = "Login failed";
                    if (statusCodeResult.StatusCode == 401)
                        errorMsg = "Sai username hoặc mật khẩu.";
                    apiResp = ApiResponse<EmployeeLoginResult>.Fail(errorMsg);
                }
                else
                {
                    apiResp = ApiResponse<EmployeeLoginResult>.Fail("Unexpected response format");
                }

                // Mã hóa response (cả success và error đều được mã hóa)
                // Login dùng clientId = username + platform (RSA tự động generate, KHÔNG dùng "admin-")
                // Public key đã được register khi client gọi register-client-key trong InitializeAsync
                string clientId = dto.Username + dto.Platform;
                
                string responseJson = System.Text.Json.JsonSerializer.Serialize(apiResp);
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

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.REGISTER_EMPLOYEE", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_empid", OracleDbType.Decimal).Value = DBNull.Value;
                cmd.Parameters.Add("p_full_name", OracleDbType.Varchar2).Value = dto.FullName;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email;
                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;

                // OUT param để nhận private key
                var privateKeyParam = new OracleParameter("p_private_key", OracleDbType.Clob)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(privateKeyParam);

                cmd.ExecuteNonQuery();

                // ✅ Lấy private key từ OracleClob
                string privateKey = string.Empty;
                if (privateKeyParam.Value is Oracle.ManagedDataAccess.Types.OracleClob clob && !clob.IsNull)
                {
                    using var reader = new StreamReader(clob, Encoding.UTF8);
                    privateKey = reader.ReadToEnd();
                }

                // Chuyển private key thành byte array để trả file
                var fileBytes = Encoding.UTF8.GetBytes(privateKey);
                var fileName = $"{dto.Username}_private_key.txt";

                // Trả file để web app tải xuống
                return File(fileBytes, "text/plain", fileName);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext,_connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill." });
            }
            catch (Exception ex)
            {
                return Conflict(new { message = "Lỗi khi thêm nhân viên", detail = ex.Message });
            }
        }

        [HttpPost("register-secure-encrypted")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> RegisterSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
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

                var dto = System.Text.Json.JsonSerializer.Deserialize<EmployeeRegisterDto>(plaintext);
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
                    // Lấy public key từ DB và lưu vào RsaKeyService
                    var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                    if (conn != null)
                    {
                        try
                        {
                            using var cmd = new OracleCommand("APP.GET_PUBLICKEY_BY_USERNAME", conn);
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                            var pPubKey = new OracleParameter("p_public_key", OracleDbType.Clob, ParameterDirection.Output);
                            cmd.Parameters.Add(pPubKey);
                            cmd.ExecuteNonQuery();

                            if (pPubKey.Value != DBNull.Value && pPubKey.Value != null)
                            {
                                var clobPubKey = (Oracle.ManagedDataAccess.Types.OracleClob)pPubKey.Value;
                                string publicKeyFromDb = clobPubKey.Value?.Trim() ?? "";
                                if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                                {
                                    // Normalize public key: loại bỏ whitespace và kiểm tra format
                                    string normalizedPublicKey = publicKeyFromDb
                                        .Replace("\r", "")
                                        .Replace("\n", "")
                                        .Replace(" ", "")
                                        .Replace("\t", "");
                                    
                                    // Nếu có PEM headers, extract Base64
                                    if (normalizedPublicKey.Contains("BEGIN") || normalizedPublicKey.Contains("END"))
                                    {
                                        // Extract Base64 từ PEM format
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
                        }
                        catch (Exception ex)
                        {
                            // Log error nhưng tiếp tục, sẽ throw error sau nếu không có public key
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
                        var errorObj = new { Success = false, Message = "Unauthorized" };
                        string errorJson = System.Text.Json.JsonSerializer.Serialize(errorObj);
                        var encryptedError = rsaKeyService.EncryptForClient(clientId, errorJson);
                        return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                        {
                            EncryptedKeyBlockBase64 = encryptedError.EncryptedKeyBlockBase64,
                            CipherDataBase64 = encryptedError.CipherDataBase64
                        }));
                    }

                    using var cmd = new OracleCommand("APP.REGISTER_EMPLOYEE", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_empid", OracleDbType.Decimal).Value = DBNull.Value;
                    cmd.Parameters.Add("p_full_name", OracleDbType.Varchar2).Value = dto.FullName;
                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                    cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;
                    cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email;
                    cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;

                    // OUT param để nhận private key
                    var privateKeyParam = new OracleParameter("p_private_key", OracleDbType.Clob)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(privateKeyParam);

                    cmd.ExecuteNonQuery();

                    // ✅ Lấy private key từ OracleClob
                    string privateKey = string.Empty;
                    if (privateKeyParam.Value is Oracle.ManagedDataAccess.Types.OracleClob clob && !clob.IsNull)
                    {
                        using var reader = new StreamReader(clob, Encoding.UTF8);
                        privateKey = reader.ReadToEnd();
                    }

                    // Tạo response object chứa private key base64 và filename
                    var responseObj = new
                    {
                        Success = true,
                        Type = "file",
                        PrivateKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(privateKey)),
                        FileName = $"{dto.Username}_private_key.txt"
                    };

                    string responseJson = System.Text.Json.JsonSerializer.Serialize(responseObj);
                    var encryptedResponse = rsaKeyService.EncryptForClient(clientId, responseJson);
                    return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                    {
                        EncryptedKeyBlockBase64 = encryptedResponse.EncryptedKeyBlockBase64,
                        CipherDataBase64 = encryptedResponse.CipherDataBase64
                    }));
                }
                catch (Exception ex)
                {
                    // Nếu có lỗi, mã hóa error message
                    var errorObj = new { Success = false, Message = ex.Message };
                    string errorJson = System.Text.Json.JsonSerializer.Serialize(errorObj);
                    var encryptedError = rsaKeyService.EncryptForClient(clientId, errorJson);
                    return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                    {
                        EncryptedKeyBlockBase64 = encryptedError.EncryptedKeyBlockBase64,
                        CipherDataBase64 = encryptedError.CipherDataBase64
                    }));
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

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.UNLOCK_DB_USER", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Tài khoản {dto.Username} đã được unlock." });
            }
            catch (OracleException ex)
            {
                return Conflict(new { message = $"Lỗi Oracle: {ex.Message}", number = ex.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        

        [HttpPost("lock")]
        [Authorize]
        public IActionResult LockUser([FromBody] UnlockEmployeeDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Username))
                return BadRequest(new { message = "Username không hợp lệ." });

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.LOCK_DB_USER", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Tài khoản {dto.Username} đã bị khóa." });
            }
            catch (OracleException ex)
            {
                return Conflict(new { message = $"Lỗi Oracle: {ex.Message}", number = ex.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // DTOs
        public class UpdatePublicKeyDto
        {
            public string? Username { get; set; }
            public string? PublicKeyBase64 { get; set; }
        }
    }
}
