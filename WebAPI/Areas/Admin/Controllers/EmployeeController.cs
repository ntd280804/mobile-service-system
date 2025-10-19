using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Data;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;

        public EmployeeController(AppDbContext context,
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper)
        {
            _context = context;
            _connManager = connManager;
            _jwtHelper = jwtHelper;
        }

        // =====================
        // 🟢 GET ALL EMPLOYEES
        // =====================
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
                return Unauthorized(new { message = "Missing Oracle session headers" });

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
                return Unauthorized(new { message = "Phiên Oracle của bạn đã bị terminate, vui lòng đăng nhập lại." });

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
                        Status = reader.GetString(5)
                    });
                }

                return Ok(list);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _connManager.RemoveConnection(username, platform, sessionId);
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
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
                return Unauthorized();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
            {
                return Unauthorized(new { message = "Phiên Oracle đã bị terminate." });
            }

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
                HandleSessionKilled(username, platform, sessionId);
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
        public async Task<IActionResult> Login([FromBody] EmployeeLoginDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            string datasource = "10.147.20.157:1521/ORCLPDB1";

            try
            {
                string platform = dto.Platform;
                string sessionId = Guid.NewGuid().ToString();

                // Tạo connection Oracle riêng cho session này
                var conn = _connManager.CreateConnection(dto.Username, dto.Password, datasource, platform, sessionId);

                // Gọi procedure kiểm tra đăng nhập
                var usernameParam = new OracleParameter("p_username", dto.Username);
                var passwordParam = new OracleParameter("p_password", dto.Password);
                var outUsernameParam = new OracleParameter("p_out_username", OracleDbType.Varchar2, 100) { Direction = ParameterDirection.Output };
                var outRoleParam = new OracleParameter("p_out_role", OracleDbType.Varchar2, 50) { Direction = ParameterDirection.Output };
                var outResultParam = new OracleParameter("p_out_result", OracleDbType.Varchar2, 50) { Direction = ParameterDirection.Output };

                await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN APP.LOGIN_EMPLOYEE(:p_username, :p_password, :p_out_username, :p_out_result, :p_out_role); END;",
                    usernameParam, passwordParam, outUsernameParam, outResultParam, outRoleParam
                );

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
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            HttpContext.Session.Clear();

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(platform) && !string.IsNullOrEmpty(sessionId))
            {
                _connManager.RemoveConnection(username, platform, sessionId);
                Console.WriteLine($"[Logout] Removed Oracle connection ({username}, {platform}, {sessionId})");
            }

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

            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
            {
                HttpContext.Session.Clear();
                return Unauthorized(new { message = "Phiên Oracle đã bị terminate, vui lòng đăng nhập lại." });
            }

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

                cmd.ExecuteNonQuery();

                return Ok(new { message = "Thêm nhân viên thành công." });
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                HandleSessionKilled(username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill." });
            }
            catch (Exception ex)
            {
                return Conflict(new { message = "Lỗi khi thêm nhân viên", detail = ex.Message });
            }
        }

        // =====================
        // ⚙️ Tiện ích nội bộ
        // =====================
        private void HandleSessionKilled(string username, string platform, string sessionId)
        {
            HttpContext.Session.Clear();
            _connManager.RemoveConnection(username, platform, sessionId);
            Console.WriteLine($"[HandleSessionKilled] Session killed for {username}-{platform}-{sessionId}");
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
