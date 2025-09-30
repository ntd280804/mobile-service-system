using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : ControllerBase
    {
        private readonly MobileDbContext _context;

        public EmployeeController(MobileDbContext context)
        {
            _context = context;
        }

        // GET: api/employee
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var employees = await _context.Employees.ToListAsync();
            return Ok(employees);
        }

        // GET: api/employee/1
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(decimal id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp == null) return NotFound();
            return Ok(emp);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] EmployeeLoginDto dto)
        {
            if (dto == null) return BadRequest("Invalid data");

            try
            {
                var usernameParam = new Oracle.ManagedDataAccess.Client.OracleParameter("p_username", dto.Username);
                var passwordParam = new Oracle.ManagedDataAccess.Client.OracleParameter("p_password", dto.Password);

                // 3 OUT parameters
                var outUsernameParam = new Oracle.ManagedDataAccess.Client.OracleParameter("p_out_username", OracleDbType.Varchar2, 100)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                var outRoleParam = new Oracle.ManagedDataAccess.Client.OracleParameter("p_out_role", OracleDbType.Varchar2, 50)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                var outResultParam = new Oracle.ManagedDataAccess.Client.OracleParameter("p_out_result", OracleDbType.Varchar2, 50)
                {
                    Direction = System.Data.ParameterDirection.Output
                };

                await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN LOGIN_EMPLOYEE(:p_username, :p_password, :p_out_username, :p_out_role, :p_out_result); END;",
                    usernameParam,
                    passwordParam,
                    outUsernameParam,
                    outRoleParam,
                    outResultParam
                );

                return Ok(new
                {
                    username = outUsernameParam.Value?.ToString(),
                    role = outRoleParam.Value?.ToString(),
                    result = outResultParam.Value?.ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Login failed", detail = ex.Message });
            }
        }



        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] EmployeeRegisterDto dto)
        {
            if (dto == null) return BadRequest("Invalid data");

            try
            {
                var result = await _context.Database.ExecuteSqlRawAsync(
                    "BEGIN REGISTER_EMPLOYEE(:fullName, :username, :password, :email, :phone, :role); END;",
                    new Oracle.ManagedDataAccess.Client.OracleParameter("fullName", dto.FullName),
                    new Oracle.ManagedDataAccess.Client.OracleParameter("username", dto.Username),
                    new Oracle.ManagedDataAccess.Client.OracleParameter("password", dto.Password),
                    new Oracle.ManagedDataAccess.Client.OracleParameter("email", dto.Email),
                    new Oracle.ManagedDataAccess.Client.OracleParameter("phone", dto.Phone),
                    new Oracle.ManagedDataAccess.Client.OracleParameter("role", dto.Role)
                );

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
                    "BEGIN UNLOCK_EMPLOYEE(:p_username, :p_out_result); END;",
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
            public string Role { get; set; }
        }
    }
}
