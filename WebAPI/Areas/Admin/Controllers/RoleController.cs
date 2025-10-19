using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Services;
using WebAPI.Helpers;
namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;

        public RoleController(OracleConnectionManager connManager, JwtHelper jwtHelper)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
        }

        // GET: api/Admin/Role/users
        [HttpGet("users")]
        [Authorize]
        public IActionResult GetAllDBUser()
        {
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
                return Unauthorized(new { message = "Oracle session expired. Please login again." });

            try
            {
                using var cmd = new OracleCommand("SELECT USERNAME FROM DBA_USERS", conn);
                using var reader = cmd.ExecuteReader();

                var list = new List<object>();
                while (reader.Read())
                {
                    list.Add(new { Username = reader.GetString(0) });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get users", detail = ex.Message });
            }
        }

        // GET: api/Admin/Role/roles
        [HttpGet("roles")]
        [Authorize]
        public IActionResult GetAllRoles()
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
                using var cmd = new OracleCommand(
                    "SELECT ROLE FROM DBA_ROLES",
                    conn
                );
                cmd.CommandType = CommandType.Text;

                cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username.ToUpper();

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
            catch (OracleException ex) when (ex.Number == 28)
            {
                _connManager.RemoveConnection(username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"[GetAllRoles] Error: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách role", detail = ex.Message });
            }
        }

        [HttpGet("roles-of-user/{username}")]
        public IActionResult GetRoleOfUser(string Username)
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
                
                string sql = "SELECT GRANTED_ROLE FROM DBA_ROLE_PRIVS WHERE GRANTEE = :username";
                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("username", Username.ToUpper()));
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
        // POST: api/Admin/Role/createrole/{roleName}
        [HttpPost("createrole/{roleName}")]
        [Authorize]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
                return Unauthorized(new { message = "Oracle session expired. Please login again." });

            try
            {
                var sql = $"CREATE ROLE \"{roleName}\"";
                using var cmd = new OracleCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"Role '{roleName}' created successfully." });
            }
            catch (OracleException ex) when (ex.Number == 1920)
            {
                return Conflict(new { message = $"Role '{roleName}' already exists." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to create role.", detail = ex.Message });
            }
        }

        // DELETE: api/Admin/Role/deleterole/{roleName}
        [HttpDelete("deleterole/{roleName}")]
        [Authorize]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
                return Unauthorized(new { message = "Oracle session expired. Please login again." });

            try
            {
                var sql = $"DROP ROLE \"{roleName}\"";
                using var cmd = new OracleCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"Role '{roleName}' deleted successfully." });
            }
            catch (OracleException ex) when (ex.Number == 1919)
            {
                return NotFound(new { message = $"Role '{roleName}' does not exist." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete role.", detail = ex.Message });
            }
        }

        // POST: api/Admin/Role/assignrole
        [HttpPost("assignrole")]
        [Authorize]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(new { message = "User or Role cannot be empty." });

            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
                return Unauthorized(new { message = "Oracle session expired. Please login again." });

            try
            {
                var sql = $"GRANT \"{request.RoleName}\" TO \"{request.UserName}\"";
                using var cmd = new OracleCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"Role '{request.RoleName}' assigned to user '{request.UserName}' successfully." });
            }
            catch (OracleException ex) when (ex.Number == 1918)
            {
                return NotFound(new { message = $"User '{request.UserName}' does not exist." });
            }
            catch (OracleException ex) when (ex.Number == 1919)
            {
                return NotFound(new { message = $"Role '{request.RoleName}' does not exist." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to assign role.", detail = ex.Message });
            }
        }

        public class AssignRoleRequest
        {
            public string UserName { get; set; }
            public string RoleName { get; set; }
        }

        // POST: api/Admin/Role/revokerole
        [HttpPost("revokerole")]
        [Authorize]
        public async Task<IActionResult> RevokeRole([FromBody] RevokeRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(new { message = "User or Role cannot be empty." });

            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
                return Unauthorized(new { message = "Oracle session expired. Please login again." });

            try
            {
                var sql = $"REVOKE \"{request.RoleName}\" FROM \"{request.UserName}\"";
                using var cmd = new OracleCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                return Ok(new { message = $"Role '{request.RoleName}' revoked from user '{request.UserName}' successfully." });
            }
            catch (OracleException ex) when (ex.Number == 1918)
            {
                return NotFound(new { message = $"User '{request.UserName}' does not exist." });
            }
            catch (OracleException ex) when (ex.Number == 1919)
            {
                return NotFound(new { message = $"Role '{request.RoleName}' does not exist." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to revoke role.", detail = ex.Message });
            }
        }

        public class RevokeRoleRequest
        {
            public string UserName { get; set; }
            public string RoleName { get; set; }
        }
    }
}