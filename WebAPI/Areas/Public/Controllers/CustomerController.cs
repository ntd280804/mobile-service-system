using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Data;
using WebAPI.Models;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly Helper _helper;
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context, Helper helper)
        {
            _context = context;
            _helper = helper;
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] CustomerLoginDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Invalid data" });

            // 🔐 Chuẩn bị connection string
            string connStr = $"User Id={dto.Username};Password={dto.Password};Data Source=192.168.26.138:1521/ORCLPDB1;Pooling=true";

            try
            {
                // 🪪 Bước 1: kiểm tra kết nối Oracle bằng tài khoản thực
                using (var conn = new OracleConnection(connStr))
                {
                    await conn.OpenAsync();
                }

                // 🧾 Bước 2: Gọi procedure LOGIN_CUSTOMER
                var phoneParam = new OracleParameter("p_phone", dto.Username);
                var passwordParam = new OracleParameter("p_password", dto.Password);

                var outPhoneParam = new OracleParameter("p_out_phone", OracleDbType.Varchar2, 100)
                { Direction = ParameterDirection.Output };

                var outResultParam = new OracleParameter("p_out_result", OracleDbType.Varchar2, 50)
                { Direction = ParameterDirection.Output };

                await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN LOGIN_CUSTOMER(:p_phone, :p_password, :p_out_phone, :p_out_result); END;",
                    phoneParam, passwordParam, outPhoneParam, outResultParam
                );

                var result = outResultParam.Value?.ToString();
                var username = outPhoneParam.Value?.ToString();

                if (result == "SUCCESS")
                {
                    // ✅ Lưu session Oracle để dùng sau này
                    HttpContext.Session.SetString("TempOracleUsername", dto.Username);
                    HttpContext.Session.SetString("TempOraclePassword", dto.Password);
                }

                return Ok(new
                {
                    username = username,
                    roles = "CUSTOMER",
                    result = result
                });
            }
            catch (OracleException ex)
            {
                // 🧭 Bắt lỗi Oracle (profile)
                return ex.Number switch
                {
                    1017 => Unauthorized(new { result = "FAILED", message = "Sai số điện thoại hoặc mật khẩu." }),
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
        public async Task<IActionResult> Register([FromBody] CustomerRegisterDto dto)
        {
            if (dto == null) return BadRequest("Invalid data");

            try
            {
                var phoneParam = new OracleParameter("p_phone", dto.Phone);
                var fullNameParam = new OracleParameter("p_full_name", dto.FullName);
                var passwordParam = new OracleParameter("p_password", dto.Password);
                var emailParam = new OracleParameter("p_email", dto.Email);
                var addressParam = new OracleParameter("p_address", dto.Address);

                await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN APP.REGISTER_CUSTOMER(:p_phone, :p_full_name, :p_password, :p_email, :p_address); END;",
                    phoneParam, fullNameParam, passwordParam, emailParam, addressParam
                );

                return Ok(new { message = "Customer registered" });
            }
            catch (Exception ex)
            {
                return Conflict(new { message = "Register failed", detail = ex.Message });
            }
        }




        // POST: api/Admin/Customer/logout
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            try
            {
                // Xóa session tạm Oracle username/password
                HttpContext.Session.Remove("TempOracleUsername");
                HttpContext.Session.Remove("TempOraclePassword");

                // Nếu muốn, xóa tất cả session
                // HttpContext.Session.Clear();

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
        
        public class CustomerLoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
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
