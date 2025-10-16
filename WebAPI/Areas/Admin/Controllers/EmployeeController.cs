using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI;
using WebAPI.Data;
using WebAPI.Models;
namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly Helper _helper;
        private readonly AppDbContext _context;

        public EmployeeController(AppDbContext context, Helper helper)
        {
            _context = context;
            _helper = helper;
        }

        // GET: api/employee
        [HttpGet]
        public IActionResult GetAll()
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get employees", detail = ex.Message });
            }
        }

        // GET: api/employee/1
        [HttpGet("{id}")]
        public IActionResult Get(decimal id)
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
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
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get employee", detail = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] EmployeeLoginDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Invalid data" });

            string connStr = $"User Id={dto.Username};Password={dto.Password};Data Source=192.168.26.138:1521/ORCLPDB1;Pooling=true";

            try
            {
                // 🪪 Bước 1: kiểm tra kết nối Oracle bằng tài khoản thực
                using (var conn = new OracleConnection(connStr))
                {
                    await conn.OpenAsync();
                }

                // 🧾 Bước 2: gọi procedure LOGIN_EMPLOYEE để kiểm tra logic nội bộ
                var usernameParam = new OracleParameter("p_username", dto.Username);
                var passwordParam = new OracleParameter("p_password", dto.Password);

                var outUsernameParam = new OracleParameter("p_out_username", OracleDbType.Varchar2, 100)
                { Direction = ParameterDirection.Output };
                var outRoleParam = new OracleParameter("p_out_role", OracleDbType.Varchar2, 50)
                { Direction = ParameterDirection.Output };
                var outResultParam = new OracleParameter("p_out_result", OracleDbType.Varchar2, 50)
                { Direction = ParameterDirection.Output };

                await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN APP.LOGIN_EMPLOYEE(:p_username, :p_password, :p_out_username, :p_out_result, :p_out_role); END;",
                    usernameParam, passwordParam, outUsernameParam, outResultParam, outRoleParam
                );

                var result = outResultParam.Value?.ToString();
                var username = outUsernameParam.Value?.ToString();
                var rolesString = outRoleParam.Value?.ToString();

                if (result == "SUCCESS")
                {
                    // ✅ Lưu session Oracle để dùng sau này
                    HttpContext.Session.SetString("TempOracleUsername", dto.Username);
                    HttpContext.Session.SetString("TempOraclePassword", dto.Password);
                }

                return Ok(new
                {
                    username = username,
                    roles = rolesString,
                    result = result
                });
            }
            catch (OracleException ex)
            {
                // 🧭 Bắt lỗi profile Oracle
                return ex.Number switch
                {
                    1017 => Unauthorized(new { result = "FAILED", message = "Sai username hoặc mật khẩu." }),
                    28000 => Unauthorized(new { result = "LOCKED", message = "Tài khoản đã bị khóa (profile)." }),
                    28001 => Unauthorized(new { result = "EXPIRED", message = "Mật khẩu đã hết hạn (profile)." }),
                    _ => StatusCode(500, new { result = "FAILED", message = $"Oracle error {ex.Number}: {ex.Message}" })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { result = "FAILED", message = ex.Message });
            }
        }





        [HttpPost("register")]
        public IActionResult Register([FromBody] EmployeeRegisterDto dto)
        {
            if (dto == null) return BadRequest("Invalid data");

            try
            {
                using var conn = _helper.GetTempOracleConnection(); // dùng session temp Oracle user/password
                var cmd = new OracleCommand("APP.REGISTER_EMPLOYEE", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_empid", OracleDbType.Decimal).Value = DBNull.Value; // để Oracle tự sinh
                cmd.Parameters.Add("p_full_name", OracleDbType.Varchar2).Value = dto.FullName;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Username;
                cmd.Parameters.Add("p_password", OracleDbType.Varchar2).Value = dto.Password;
                cmd.Parameters.Add("p_email", OracleDbType.Varchar2).Value = dto.Email;
                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = dto.Phone;

                cmd.ExecuteNonQuery();

                return Ok(new { message = "Employee registered" });
            }
            catch (Exception ex)
            {
                return Conflict(new { message = "Register failed", detail = ex.Message });
            }
        }

        [HttpPost("unlock")]
        public async Task<IActionResult> Unlock([FromBody] UnlockEmployeeDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Username))
                return BadRequest(new { message = "Invalid data" });

            try
            {
                var usernameParam = new OracleParameter("p_username", dto.Username);

                var outResultParam = new OracleParameter("p_out_result", OracleDbType.Varchar2, 50)
                {
                    Direction = System.Data.ParameterDirection.Output
                };

                await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN APP.UNLOCK_EMPLOYEE(:p_username, :p_out_result); END;",
                    usernameParam,
                    outResultParam
                );

                return Ok(new
                {
                    username = dto.Username,
                    result = outResultParam.Value?.ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unlock failed", detail = ex.Message });
            }
        }// POST: api/Admin/Employee/logout
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromServices] IHubContext<AuthHub> hub)
        {
            try
            {
                var username = HttpContext.Session.GetString("TempOracleUsername");
                // Xóa session tạm Oracle username/password
                HttpContext.Session.Remove("TempOracleUsername");
                HttpContext.Session.Remove("TempOraclePassword");

                // Nếu muốn, xóa tất cả session
                // HttpContext.Session.Clear();
                if (!string.IsNullOrEmpty(username))
                {
                    await hub.Clients.Group(username).SendAsync("ForceLogout");
                    Console.WriteLine($"📡 Gửi ForceLogout tới group: {username}");
                }
                return Ok(new
                {
                    message = "Logout thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Logout thất bại",
                    detail = ex.Message
                });
            }
        }

        // DTO
        public class UnlockEmployeeDto
        {
            public string Username { get; set; }
        }
        public class EmployeeLoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
        public class EmployeeRegisterDto
        {
            public string FullName { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
        }
    }
}
