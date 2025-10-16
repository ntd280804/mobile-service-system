using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Data;
using static WebAPI.Areas.Admin.Controllers.EmployeeController;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly Helper _helper;

        public RoleController( Helper helper)
        {
            _helper = helper;
        }

        [HttpGet("users")]
        public IActionResult GetAllDBUser()
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand("SELECT USERNAME FROM DBA_USERS", conn);
                using var reader = cmd.ExecuteReader();

                var list = new List<object>();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        Username = reader.GetString(0)
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get users", detail = ex.Message });
            }
        }

        [HttpGet("roles")]
        public IActionResult GetAllRole()
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
                string sql = "SELECT ROLE FROM DBA_ROLES";
                using var cmd = new OracleCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                var list = new List<object>();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        Role = reader.GetString(0)
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get roles", detail = ex.Message });
            }
        }
        [HttpGet("roles-of-user/{username}")]
        public IActionResult GetRoleOfUser(string username)
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
                string sql = "SELECT GRANTED_ROLE FROM DBA_ROLE_PRIVS WHERE GRANTEE = :username";
                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("username", username.ToUpper()));
                using var reader = cmd.ExecuteReader();

                var list = new List<object>();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        GrantedRole = reader.GetString(0)
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get roles of user", detail = ex.Message });
            }
        }

        [HttpPost("createrole/{roleName}")]
        public async Task<IActionResult> createrole(string roleName)
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();

                // 📌 Tạo câu lệnh SQL an toàn (có escape)
                var sql = $"CREATE ROLE \"{roleName}\"";  // dùng dấu " để tránh lỗi nếu có chữ hoa/thường

                using var cmd = new OracleCommand(sql, conn);
                var result = await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"✅ Đã tạo role '{roleName}' thành công." });
            }
            catch (OracleException ex) when (ex.Number == 1920) // ORA-01920: role name conflicts with another user or role name
            {
                return Conflict(new { message = $"❌ Role '{roleName}' đã tồn tại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "❌ Tạo role thất bại.", detail = ex.Message });
            }
        }
        [HttpDelete("deleterole/{roleName}")]
        public async Task<IActionResult> deleterole(string roleName)
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();

                // 📌 Tạo câu lệnh SQL xóa role
                var sql = $"DROP ROLE \"{roleName}\"";

                using var cmd = new OracleCommand(sql, conn);
                var result = await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"✅ Đã xóa role '{roleName}' thành công." });
            }
            catch (OracleException ex) when (ex.Number == 1919) // ORA-01919: role does not exist
            {
                return NotFound(new { message = $"❌ Role '{roleName}' không tồn tại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "❌ Xóa role thất bại.", detail = ex.Message });
            }
        }
        [HttpPost("assignrole")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.RoleName))
            {
                return BadRequest(new { message = "❌ User hoặc Role không được để trống." });
            }

            try
            {
                using var conn = _helper.GetTempOracleConnection();

                // 📌 Lệnh SQL: gán role cho user
                var sql = $"GRANT \"{request.RoleName}\" TO \"{request.UserName}\"";

                using var cmd = new OracleCommand(sql, conn);
                var result = await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"✅ Đã gán role '{request.RoleName}' cho user '{request.UserName}' thành công." });
            }
            catch (OracleException ex) when (ex.Number == 1918) // ORA-01918: user does not exist
            {
                return NotFound(new { message = $"❌ User '{request.UserName}' không tồn tại." });
            }
            catch (OracleException ex) when (ex.Number == 1919) // ORA-01919: role does not exist
            {
                return NotFound(new { message = $"❌ Role '{request.RoleName}' không tồn tại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "❌ Gán role thất bại.", detail = ex.Message });
            }
        }

        public class AssignRoleRequest
        {
            public string UserName { get; set; }
            public string RoleName { get; set; }
        }
        [HttpPost("revokerole")]
        public async Task<IActionResult> RevokeRole([FromBody] RevokeRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.RoleName))
            {
                return BadRequest(new { message = "❌ User hoặc Role không được để trống." });
            }

            try
            {
                using var conn = _helper.GetTempOracleConnection();

                // 📌 Lệnh SQL thu hồi role
                var sql = $"REVOKE \"{request.RoleName}\" FROM \"{request.UserName}\"";

                using var cmd = new OracleCommand(sql, conn);
                var result = await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"✅ Đã thu hồi role '{request.RoleName}' khỏi user '{request.UserName}' thành công." });
            }
            catch (OracleException ex) when (ex.Number == 1918) // ORA-01918: user does not exist
            {
                return NotFound(new { message = $"❌ User '{request.UserName}' không tồn tại." });
            }
            catch (OracleException ex) when (ex.Number == 1919) // ORA-01919: role does not exist
            {
                return NotFound(new { message = $"❌ Role '{request.RoleName}' không tồn tại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "❌ Thu hồi role thất bại.", detail = ex.Message });
            }
        }

        public class RevokeRoleRequest
        {
            public string UserName { get; set; }
            public string RoleName { get; set; }
        }


    }
}
