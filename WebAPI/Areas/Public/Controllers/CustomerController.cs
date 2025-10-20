using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;

using WebAPI.Helpers;

using WebAPI.Services;
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
        public IActionResult Login([FromBody] CustomerLoginDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Invalid data" });

            try
            {
                string platform = dto.Platform;
                string sessionId = Guid.NewGuid().ToString();
                var conn = _connManager.CreateConnection(dto.Username, dto.Password, platform, sessionId);

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
                var roles = "CUSTOMER";

                if (result != "SUCCESS")
                    return Unauthorized(new { result, message = "Sai số điện thoại hoặc mật khẩu." });

                var token = _jwtHelper.GenerateToken(username, roles, sessionId);
                return Ok(new { username, roles, result, sessionId, token });
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
                return StatusCode(500, new { result = "FAILED", message = ex.Message });
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

        public class CustomerLoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Platform { get; set; }
        }
        public class CustomerRegisterDto
        {
            public string FullName { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
        }
    }
}