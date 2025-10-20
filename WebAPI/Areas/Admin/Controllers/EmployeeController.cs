
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;

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
                        FullName = reader.GetString(1),
                        Username = reader.GetString(2),
                        Email = reader.GetString(3),
                        Phone = reader.GetString(4),
                        Status = reader.GetString(5),
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
                        FullName = reader.GetString(1),
                        Username = reader.GetString(2),
                        Email = reader.GetString(3),
                        Phone = reader.GetString(4),
                        Status = reader.GetString(5)
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
        public IActionResult Login([FromBody] EmployeeLoginDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            try
            {
                string platform = dto.Platform;
                string sessionId = Guid.NewGuid().ToString();

                // Tạo connection Oracle riêng cho session này
                var conn = _connManager.CreateConnection(dto.Username, dto.Password, platform, sessionId);

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
                    return Unauthorized(new { result, message = "Đăng nhập thất bại." });

                // Tạo JWT token
                var token = _jwtHelper.GenerateToken(username, roles, sessionId);

                return Ok(new { username, roles, result, sessionId, token });
            }
            catch (OracleException ex)
            {
                return ex.Number switch
                {
                    1017 => Unauthorized(new { result = "FAILED", message = "Sai username hoặc mật khẩu." }),
                    28000 => Unauthorized(new { result = "LOCKED", message = "Tài khoản bị khóa." }),
                    28001 => Unauthorized(new { result = "EXPIRED", message = "Mật khẩu đã hết hạn." }),
                    _ => StatusCode(500, new { result = "FAILED", message = $"Oracle error {ex.Number}: {ex.Message}" })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { result = "FAILED", message = ex.Message });
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

        // =====================
        // DTO
        // =====================
        public class EmployeeLoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Platform { get; set; } // "WEB" hoặc "MOBILE"
        }

        public class EmployeeRegisterDto
        {
            public string FullName { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
        }

        public class UnlockEmployeeDto
        {
            public string Username { get; set; }
        }
    }
}
