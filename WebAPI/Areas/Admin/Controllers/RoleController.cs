using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Models.Permission;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public RoleController(ControllerHelper helper)
        {
            _helper = helper;
        }

        [HttpGet("users")]
        [Authorize]
        public IActionResult GetAllDBUser()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_USERS", "p_cursor",
                    reader => new { Username = reader.GetString(0) });
                return Ok(list);
            }, "Failed to get users");
        }

        [HttpGet("roles")]
        [Authorize]
        public IActionResult GetAllRoles()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_ROLES", "p_cursor",
                    reader => new { Role = reader.GetString(0) });
                return Ok(list);
            }, "Lỗi khi lấy danh sách role");
        }

        [HttpGet("roles-of-user/{Username}")]
        [Authorize]
        public IActionResult GetRoleOfUser(string Username)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ROLES_OF_USER", "p_cursor",
                    reader => new { GrantedRole = reader.GetString(0) },
                    ("p_username", OracleDbType.Varchar2, Username.ToUpper()));
                return Ok(list);
            }, "Failed to get roles of user");
        }

        [HttpPost("createrole/{roleName}")]
        [Authorize]
        public IActionResult CreateRole(string roleName)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                try
                {
                    OracleHelper.ExecuteNonQuery(conn, "APP.CREATE_ROLE_PROC",
                        ("p_role_name", OracleDbType.Varchar2, roleName));
                    return Ok(new { message = $"Role '{roleName}' created successfully." });
                }
                catch (OracleException ex) when (ex.Number == 1920)
                {
                    return Conflict(new { message = $"Role '{roleName}' already exists." });
                }
            }, "Failed to create role.");
        }

        [HttpDelete("deleterole/{roleName}")]
        [Authorize]
        public IActionResult DeleteRole(string roleName)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                try
                {
                    OracleHelper.ExecuteNonQuery(conn, "APP.DELETE_ROLE_PROC",
                        ("p_role_name", OracleDbType.Varchar2, roleName));
                    return Ok(new { message = $"Role '{roleName}' deleted successfully." });
                }
                catch (OracleException ex) when (ex.Number == 1919)
                {
                    return NotFound(new { message = $"Role '{roleName}' does not exist." });
                }
            }, "Failed to delete role.");
        }

        [HttpPost("assignrole")]
        [Authorize]
        public IActionResult AssignRole([FromBody] AssignRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(new { message = "User or Role cannot be empty." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                try
                {
                    OracleHelper.ExecuteNonQuery(conn, "APP.ASSIGN_ROLE_PROC",
                        ("p_username", OracleDbType.Varchar2, request.UserName),
                        ("p_role_name", OracleDbType.Varchar2, request.RoleName));
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
            }, "Failed to assign role.");
        }

        [HttpPost("revokerole")]
        [Authorize]
        public IActionResult RevokeRole([FromBody] RevokeRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(new { message = "User or Role cannot be empty." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                try
                {
                    OracleHelper.ExecuteNonQuery(conn, "APP.REVOKE_ROLE_PROC",
                        ("p_username", OracleDbType.Varchar2, request.UserName),
                        ("p_role_name", OracleDbType.Varchar2, request.RoleName));
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
            }, "Failed to revoke role.");
        }
    }
}
