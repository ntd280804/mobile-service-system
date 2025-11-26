using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Linq;
using WebAPI.Helpers;
using WebAPI.Models.Permission;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public ProfileController(ControllerHelper helper)
        {
            _helper = helper;
        }

        [HttpGet("users")]
        [Authorize]
        public IActionResult GetAllUsersWithProfile()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_USERS_WITH_PROFILE", "cur_out",
                    reader => new
                    {
                        Username = reader["USERNAME"].ToString(),
                        Profile = reader["PROFILE"].ToString()
                    });
                return Ok(list);
            }, "Failed to get users");
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAllProfiles()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var tempList = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_PROFILES_WITH_LIMITS", "cur_out",
                    reader => new
                    {
                        Profile = reader["PROFILE"].ToString(),
                        ResourceName = reader["RESOURCE_NAME"].ToString(),
                        Limit = reader["LIMIT"].ToString()
                    });

                var grouped = tempList
                    .GroupBy(x => x.Profile)
                    .Select(g => new
                    {
                        ProfileName = g.Key,
                        IdleTime = g.FirstOrDefault(r => r.ResourceName == "IDLE_TIME")?.Limit,
                        ConnectTime = g.FirstOrDefault(r => r.ResourceName == "CONNECT_TIME")?.Limit,
                        FailedLogin = g.FirstOrDefault(r => r.ResourceName == "FAILED_LOGIN_ATTEMPTS")?.Limit,
                        LockTime = g.FirstOrDefault(r => r.ResourceName == "PASSWORD_LOCK_TIME")?.Limit,
                        InactiveAccountTime = g.FirstOrDefault(r => r.ResourceName == "INACTIVE_ACCOUNT_TIME")?.Limit
                    });

                return Ok(grouped);
            }, "Failed to get profiles");
        }

        [HttpGet("{profileName}")]
        [Authorize]
        public IActionResult GetProfileByName(string profileName)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var dict = new Dictionary<string, string>();

                var rows = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PROFILE_BY_NAME", "cur_out",
                    reader => new
                {
                        ResourceName = reader["RESOURCE_NAME"].ToString(),
                        Limit = reader["LIMIT"].ToString()
                    },
                    ("p_profile_name", OracleDbType.Varchar2, profileName.ToUpper()));

                foreach (var row in rows)
                {
                    dict[row.ResourceName] = row.Limit;
                }

                if (dict.Count == 0)
                    return NotFound(new { message = $"Profile '{profileName}' not found" });

                var result = new
                {
                    ProfileName = profileName.ToUpper(),
                    IdleTime = dict.ContainsKey("IDLE_TIME") ? dict["IDLE_TIME"] : null,
                    ConnectTime = dict.ContainsKey("CONNECT_TIME") ? dict["CONNECT_TIME"] : null,
                    FailedLogin = dict.ContainsKey("FAILED_LOGIN_ATTEMPTS") ? dict["FAILED_LOGIN_ATTEMPTS"] : null,
                    LockTime = dict.ContainsKey("PASSWORD_LOCK_TIME") ? dict["PASSWORD_LOCK_TIME"] : null,
                    InactiveAccountTime = dict.ContainsKey("INACTIVE_ACCOUNT_TIME") ? dict["INACTIVE_ACCOUNT_TIME"] : null
                };

                return Ok(result);
            }, "Failed to get profile details");
        }

        [HttpPost]
        [Authorize]
        public IActionResult CreateProfile([FromBody] CreateProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ProfileName)) return BadRequest(new { message = "ProfileName is required" });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                try
                {
                    OracleHelper.ExecuteNonQuery(conn, "APP.CREATE_PROFILE",
                        ("p_profile_name", OracleDbType.Varchar2, request.ProfileName.Trim().ToUpper()),
                        ("p_idle_time", OracleDbType.Varchar2, request.IdleTime.Trim().ToUpper()),
                        ("p_connect_time", OracleDbType.Varchar2, request.ConnectTime.Trim().ToUpper()),
                        ("p_failed_login", OracleDbType.Varchar2, request.FailedLogin.Trim().ToUpper()),
                        ("p_lock_time", OracleDbType.Varchar2, request.LockTime.Trim().ToUpper()),
                        ("p_inactive_account_time", OracleDbType.Varchar2, request.InactiveAccountTime.Trim().ToUpper()));

                return Ok(new { message = $"Profile '{request.ProfileName}' created successfully" });
            }
                catch (OracleException ex) when (ex.Number == 2379)
            {
                    return Conflict(new { message = $"Profile '{request.ProfileName}' already exists", detail = ex.Message });
            }
            }, "Failed to create profile");
        }

        [HttpDelete("{profileName}")]
        [Authorize]
        public IActionResult DeleteProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return BadRequest(new { message = "Profile name is required" });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
            try
            {
                    OracleHelper.ExecuteNonQuery(conn, "APP.DELETE_PROFILE",
                        ("p_profile_name", OracleDbType.Varchar2, profileName.Trim().ToUpper()));

                return Ok(new { message = $"Profile '{profileName}' deleted successfully" });
            }
                catch (OracleException ex) when (ex.Number == 2380)
            {
                    return NotFound(new { message = $"Profile '{profileName}' does not exist", detail = ex.Message });
            }
            }, "Failed to delete profile");
        }


        [HttpPost("assign")]
        [Authorize]
        public IActionResult AssignProfileToUser([FromBody] AssignProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.ProfileName))
                return BadRequest(new { message = "Username and ProfileName are required" });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                try
                {
                    OracleHelper.ExecuteNonQuery(conn, "APP.ASSIGN_PROFILE_TO_USER",
                        ("p_username", OracleDbType.Varchar2, request.Username.Trim()),
                        ("p_profile_name", OracleDbType.Varchar2, request.ProfileName.Trim().ToUpper()));

                return Ok(new { message = $"Profile '{request.ProfileName}' has been assigned to user '{request.Username}'" });
            }
                catch (OracleException ex) when (ex.Number == 2379)
            {
                    return NotFound(new { message = $"Profile '{request.ProfileName}' does not exist", oracleError = ex.Number, detail = ex.Message });
                }
                catch (OracleException ex) when (ex.Number == 1918)
                {
                    return NotFound(new { message = $"User '{request.Username}' does not exist", oracleError = ex.Number, detail = ex.Message });
            }
            }, "Failed to assign profile");
        }


        [HttpPut("{profileName}")]
        [Authorize]
        public IActionResult UpdateProfile(string profileName, [FromBody] UpdateProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return BadRequest(new { message = "Profile name is required" });
            if (request == null) return BadRequest(new { message = "Request body is required" });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.UPDATE_PROFILE";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.Add("p_profile_name", OracleDbType.Varchar2, profileName.Trim().ToUpper(), System.Data.ParameterDirection.Input);
                
                var idleTimeParam = new OracleParameter("p_idle_time", OracleDbType.Varchar2);
                idleTimeParam.Value = string.IsNullOrWhiteSpace(request.IdleTime) ? DBNull.Value : (object)request.IdleTime.Trim().ToUpper();
                cmd.Parameters.Add(idleTimeParam);

                var connectTimeParam = new OracleParameter("p_connect_time", OracleDbType.Varchar2);
                connectTimeParam.Value = string.IsNullOrWhiteSpace(request.ConnectTime) ? DBNull.Value : (object)request.ConnectTime.Trim().ToUpper();
                cmd.Parameters.Add(connectTimeParam);

                var failedLoginParam = new OracleParameter("p_failed_login", OracleDbType.Varchar2);
                failedLoginParam.Value = string.IsNullOrWhiteSpace(request.FailedLogin) ? DBNull.Value : (object)request.FailedLogin.Trim().ToUpper();
                cmd.Parameters.Add(failedLoginParam);

                var lockTimeParam = new OracleParameter("p_lock_time", OracleDbType.Varchar2);
                lockTimeParam.Value = string.IsNullOrWhiteSpace(request.LockTime) ? DBNull.Value : (object)request.LockTime.Trim().ToUpper();
                cmd.Parameters.Add(lockTimeParam);

                var inactiveAccountTimeParam = new OracleParameter("p_inactive_account_time", OracleDbType.Varchar2);
                inactiveAccountTimeParam.Value = string.IsNullOrWhiteSpace(request.InactiveAccountTime) ? DBNull.Value : (object)request.InactiveAccountTime.Trim().ToUpper();
                cmd.Parameters.Add(inactiveAccountTimeParam);

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Profile '{profileName}' updated successfully" });
            }
                catch (OracleException ex) when (ex.Number == 2380)
            {
                    return NotFound(new { message = $"Profile '{profileName}' does not exist", detail = ex.Message });
            }
            }, "Failed to update profile");
        }
    }
}
