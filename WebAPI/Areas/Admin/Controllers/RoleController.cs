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
        private readonly OracleSessionHelper _oracleSessionHelper;

        public RoleController(OracleConnectionManager connManager, JwtHelper jwtHelper, OracleSessionHelper oracleSessionHelper)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
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
                using var cmd = new OracleCommand("APP.GET_ALL_USERS", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

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
                using var cmd = new OracleCommand("APP.GET_ALL_ROLES", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();
                while (reader.Read())
                {
                    list.Add(new { Role = reader.GetString(0) });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAllRoles] Error: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách role", detail = ex.Message });
            }
        }

        [HttpGet("roles-of-user/{Username}")]
        [Authorize]
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
                using var cmd = new OracleCommand("APP.GET_ROLES_OF_USER", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = Username.ToUpper();
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();
                while (reader.Read())
                {
                    list.Add(new { GrantedRole = reader.GetString(0) });
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
                using var cmd = new OracleCommand("APP.CREATE_ROLE_PROC", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2).Value = roleName;

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
                using var cmd = new OracleCommand("APP.DELETE_ROLE_PROC", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2).Value = roleName;

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
                using var cmd = new OracleCommand("APP.ASSIGN_ROLE_PROC", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = request.UserName;
                cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2).Value = request.RoleName;

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
                using var cmd = new OracleCommand("APP.REVOKE_ROLE_PROC", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = request.UserName;
                cmd.Parameters.Add("p_role_name", OracleDbType.Varchar2).Value = request.RoleName;

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