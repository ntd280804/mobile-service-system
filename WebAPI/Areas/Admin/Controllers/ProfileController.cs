using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Linq;
using WebAPI.Helpers;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public ProfileController(OracleConnectionManager connManager, JwtHelper jwtHelper, OracleSessionHelper oracleSessionHelper)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
        }

        private OracleConnection GetConnectionOrUnauthorized(out IActionResult unauthorizedResult)
        {
            unauthorizedResult = null;
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(sessionId))
            {
                unauthorizedResult = Unauthorized(new { message = "Missing Oracle session headers" });
                return null;
            }

            var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
            {
                unauthorizedResult = Unauthorized(new { message = "Oracle session expired. Please login again." });
                return null;
            }

            return conn;
        }

        [HttpGet("users")]
        [Authorize]
        public IActionResult GetAllUsersWithProfile()
        {
            if (GetConnectionOrUnauthorized(out var unauthorized) is not OracleConnection conn) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_USERS_WITH_PROFILE";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                var curParam = new OracleParameter("cur_out", OracleDbType.RefCursor, System.Data.ParameterDirection.Output);
                cmd.Parameters.Add(curParam);

                var list = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new
                    {
                        Username = reader["USERNAME"].ToString(),
                        Profile = reader["PROFILE"].ToString()
                    });
                }

                return Ok(list);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = "Failed to get users", oracleError = ex.Number, detail = ex.Message });
            }
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAllProfiles()
        {
            if (GetConnectionOrUnauthorized(out var unauthorized) is not OracleConnection conn) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_PROFILES_WITH_LIMITS";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                var curParam = new OracleParameter("cur_out", OracleDbType.RefCursor, System.Data.ParameterDirection.Output);
                cmd.Parameters.Add(curParam);

                var tempList = new List<dynamic>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tempList.Add(new
                    {
                        Profile = reader["PROFILE"].ToString(),
                        ResourceName = reader["RESOURCE_NAME"].ToString(),
                        Limit = reader["LIMIT"].ToString()
                    });
                }

                var grouped = tempList
                    .GroupBy(x => x.Profile)
                    .Select(g => new
                    {
                        ProfileName = g.Key,
                        FailedLogin = g.FirstOrDefault(r => r.ResourceName == "FAILED_LOGIN_ATTEMPTS")?.Limit,
                        LifeTime = g.FirstOrDefault(r => r.ResourceName == "PASSWORD_LIFE_TIME")?.Limit,
                        GraceTime = g.FirstOrDefault(r => r.ResourceName == "PASSWORD_GRACE_TIME")?.Limit,
                        LockTime = g.FirstOrDefault(r => r.ResourceName == "PASSWORD_LOCK_TIME")?.Limit
                    });

                return Ok(grouped);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = "Failed to get profiles", oracleError = ex.Number, detail = ex.Message });
            }
        }

        [HttpGet("{profileName}")]
        [Authorize]
        public IActionResult GetProfileByName(string profileName)
        {
            if (GetConnectionOrUnauthorized(out var unauthorized) is not OracleConnection conn) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_PROFILE_BY_NAME";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.Add("p_profile_name", OracleDbType.Varchar2, profileName.ToUpper(), System.Data.ParameterDirection.Input);
                cmd.Parameters.Add("cur_out", OracleDbType.RefCursor, System.Data.ParameterDirection.Output);

                var dict = new Dictionary<string, string>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    dict[reader["RESOURCE_NAME"].ToString()] = reader["LIMIT"].ToString();
                }

                if (dict.Count == 0)
                    return NotFound(new { message = $"Profile '{profileName}' not found" });

                var result = new
                {
                    ProfileName = profileName.ToUpper(),
                    FailedLogin = dict.ContainsKey("FAILED_LOGIN_ATTEMPTS") ? dict["FAILED_LOGIN_ATTEMPTS"] : null,
                    LifeTime = dict.ContainsKey("PASSWORD_LIFE_TIME") ? dict["PASSWORD_LIFE_TIME"] : null,
                    GraceTime = dict.ContainsKey("PASSWORD_GRACE_TIME") ? dict["PASSWORD_GRACE_TIME"] : null,
                    LockTime = dict.ContainsKey("PASSWORD_LOCK_TIME") ? dict["PASSWORD_LOCK_TIME"] : null
                };

                return Ok(result);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = "Failed to get profile details", oracleError = ex.Number, detail = ex.Message });
            }
        }

        public class CreateProfileRequest
        {
            public string ProfileName { get; set; } = string.Empty;
            public string FailedLogin { get; set; } = "UNLIMITED";
            public string LifeTime { get; set; } = "UNLIMITED";
            public string GraceTime { get; set; } = "UNLIMITED";
            public string LockTime { get; set; } = "UNLIMITED";
        }

        [HttpPost]
        [Authorize]
        public IActionResult CreateProfile([FromBody] CreateProfileRequest request)
        {
            if (GetConnectionOrUnauthorized(out var unauthorized) is not OracleConnection conn) return unauthorized;
            if (string.IsNullOrWhiteSpace(request.ProfileName)) return BadRequest(new { message = "ProfileName is required" });

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.CREATE_PROFILE";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.Add("p_profile_name", OracleDbType.Varchar2, request.ProfileName.Trim().ToUpper(), System.Data.ParameterDirection.Input);
                cmd.Parameters.Add("p_failed_login", OracleDbType.Varchar2, request.FailedLogin.Trim().ToUpper(), System.Data.ParameterDirection.Input);
                cmd.Parameters.Add("p_life_time", OracleDbType.Varchar2, request.LifeTime.Trim().ToUpper(), System.Data.ParameterDirection.Input);
                cmd.Parameters.Add("p_grace_time", OracleDbType.Varchar2, request.GraceTime.Trim().ToUpper(), System.Data.ParameterDirection.Input);
                cmd.Parameters.Add("p_lock_time", OracleDbType.Varchar2, request.LockTime.Trim().ToUpper(), System.Data.ParameterDirection.Input);

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Profile '{request.ProfileName}' created successfully" });
            }
            catch (OracleException ex)
            {
                if (ex.Number == 2379)
                    return Conflict(new { message = $"Profile '{request.ProfileName}' already exists", detail = ex.Message });

                return StatusCode(500, new { message = "Failed to create profile", oracleError = ex.Number, detail = ex.Message });
            }
        }

        [HttpDelete("{profileName}")]
        [Authorize]
        public IActionResult DeleteProfile(string profileName)
        {
            if (GetConnectionOrUnauthorized(out var unauthorized) is not OracleConnection conn) return unauthorized;
            if (string.IsNullOrWhiteSpace(profileName)) return BadRequest(new { message = "Profile name is required" });

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.DELETE_PROFILE";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.Add("p_profile_name", OracleDbType.Varchar2, profileName.Trim().ToUpper(), System.Data.ParameterDirection.Input);

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Profile '{profileName}' deleted successfully" });
            }
            catch (OracleException ex)
            {
                if (ex.Number == 2380)
                    return NotFound(new { message = $"Profile '{profileName}' does not exist", detail = ex.Message });

                return StatusCode(500, new { message = "Failed to delete profile", oracleError = ex.Number, detail = ex.Message });
            }
        }

        public class AssignProfileRequest
        {
            public string Username { get; set; } = string.Empty;
            public string ProfileName { get; set; } = string.Empty;
        }

        [HttpPost("assign")]
        [Authorize]
        public IActionResult AssignProfileToUser([FromBody] AssignProfileRequest request)
        {
            if (GetConnectionOrUnauthorized(out var unauthorized) is not OracleConnection conn) return unauthorized;
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.ProfileName))
                return BadRequest(new { message = "Username and ProfileName are required" });

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.ASSIGN_PROFILE_TO_USER";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.Add("p_username", OracleDbType.Varchar2, request.Username.Trim(), System.Data.ParameterDirection.Input);
                cmd.Parameters.Add("p_profile_name", OracleDbType.Varchar2, request.ProfileName.Trim().ToUpper(), System.Data.ParameterDirection.Input);

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Profile '{request.ProfileName}' has been assigned to user '{request.Username}'" });
            }
            catch (OracleException ex)
            {
                if (ex.Number == 2379)
                    return NotFound(new { message = $"Profile '{request.ProfileName}' does not exist", oracleError = ex.Number, detail = ex.Message });
                if (ex.Number == 1918)
                    return NotFound(new { message = $"User '{request.Username}' does not exist", oracleError = ex.Number, detail = ex.Message });

                return StatusCode(500, new { message = "Failed to assign profile", oracleError = ex.Number, detail = ex.Message });
            }
        }
    }
}
