using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Data;
using System.Collections.Generic;
using System.Linq;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly Helper _helper;

        public ProfileController(Helper helper)
        {
            _helper = helper;
        }

        private static readonly string[] TargetResources = new[]
        {
            "FAILED_LOGIN_ATTEMPTS",
            "PASSWORD_LIFE_TIME",
            "PASSWORD_GRACE_TIME",
            "PASSWORD_LOCK_TIME"
        };
        [HttpGet("users")]
        public IActionResult GetAllUsersWithProfile()
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand(
                    "SELECT USERNAME, PROFILE FROM DBA_USERS ORDER BY PROFILE, USERNAME",
                    conn
                );

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

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
                return StatusCode(500, new
                {
                    message = "Failed to get users with profiles",
                    oracleError = ex.Number,
                    detail = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unexpected error",
                    detail = ex.Message
                });
            }
        }

        [HttpGet]
        public IActionResult GetAllProfiles()
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand(@"
                    SELECT PROFILE, RESOURCE_NAME, LIMIT 
                    FROM DBA_PROFILES 
                    WHERE RESOURCE_NAME IN ('FAILED_LOGIN_ATTEMPTS','PASSWORD_LIFE_TIME','PASSWORD_GRACE_TIME','PASSWORD_LOCK_TIME')
                    ORDER BY PROFILE, RESOURCE_NAME
                ", conn);

                using var reader = cmd.ExecuteReader();
                var tempList = new List<dynamic>();

                while (reader.Read())
                {
                    tempList.Add(new
                    {
                        Profile = reader["PROFILE"].ToString(),
                        ResourceName = reader["RESOURCE_NAME"].ToString(),
                        Limit = reader["LIMIT"].ToString()
                    });
                }

                // Nhóm theo profile
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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get profiles", detail = ex.Message });
            }
        }

        [HttpGet("{profileName}")]
        public IActionResult GetProfileByName(string profileName)
        {
            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand(@"
                    SELECT PROFILE, RESOURCE_NAME, LIMIT 
                    FROM DBA_PROFILES 
                    WHERE PROFILE = :profileName 
                    AND RESOURCE_NAME IN ('FAILED_LOGIN_ATTEMPTS','PASSWORD_LIFE_TIME','PASSWORD_GRACE_TIME','PASSWORD_LOCK_TIME')
                ", conn);

                cmd.Parameters.Add(new OracleParameter("profileName", profileName.ToUpper()));

                using var reader = cmd.ExecuteReader();
                var dict = new Dictionary<string, string>();

                while (reader.Read())
                {
                    var resource = reader["RESOURCE_NAME"].ToString();
                    var limit = reader["LIMIT"].ToString();
                    dict[resource] = limit;
                }

                if (dict.Count == 0)
                {
                    return NotFound(new { message = $"Profile '{profileName}' not found" });
                }

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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get profile details", detail = ex.Message });
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
        public IActionResult CreateProfile([FromBody] CreateProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ProfileName))
            {
                return BadRequest(new { message = "ProfileName is required" });
            }

            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand();
                cmd.Connection = conn;

                // Làm sạch input (chuyển sang upper, trim)
                var profile = request.ProfileName.Trim().ToUpper();
                var failedLogin = request.FailedLogin.Trim().ToUpper();
                var lifeTime = request.LifeTime.Trim().ToUpper();
                var graceTime = request.GraceTime.Trim().ToUpper();
                var lockTime = request.LockTime.Trim().ToUpper();

                cmd.CommandText = $@"
                    CREATE PROFILE {profile} LIMIT
                    FAILED_LOGIN_ATTEMPTS {failedLogin}
                    PASSWORD_LIFE_TIME {lifeTime}
                    PASSWORD_GRACE_TIME {graceTime}
                    PASSWORD_LOCK_TIME {lockTime}
                ";

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = $"Profile '{profile}' created successfully",
                    profile = new
                    {
                        profileName = profile,
                        failedLogin,
                        lifeTime,
                        graceTime,
                        lockTime
                    }
                });
            }
            catch (OracleException ex)
            {
                if (ex.Number == 2379) // ORA-02379: profile already exists
                {
                    return Conflict(new
                    {
                        message = $"Profile '{request.ProfileName}' already exists",
                        detail = ex.Message
                    });
                }

                return StatusCode(500, new
                {
                    message = "Failed to create profile",
                    oracleError = ex.Number,
                    detail = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unexpected error",
                    detail = ex.Message
                });
            }
        }
        [HttpDelete("{profileName}")]
        public IActionResult DeleteProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return BadRequest(new { message = "Profile name is required" });

            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand();
                cmd.Connection = conn;

                var profile = profileName.Trim().ToUpper();
                cmd.CommandText = $"DROP PROFILE {profile} CASCADE";

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = $"Profile '{profile}' deleted successfully"
                });
            }
            catch (OracleException ex)
            {
                // ORA-02380: profile does not exist
                if (ex.Number == 2380)
                {
                    return NotFound(new
                    {
                        message = $"Profile '{profileName}' does not exist",
                        detail = ex.Message
                    });
                }

                return StatusCode(500, new
                {
                    message = "Failed to delete profile",
                    oracleError = ex.Number,
                    detail = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unexpected error",
                    detail = ex.Message
                });
            }
        }
        public class AssignProfileRequest
        {
            public string Username { get; set; } = string.Empty;
            public string ProfileName { get; set; } = string.Empty;
        }

        [HttpPost("assign")]
        public IActionResult AssignProfileToUser([FromBody] AssignProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.ProfileName))
                return BadRequest(new { message = "Username and ProfileName are required" });

            try
            {
                using var conn = _helper.GetTempOracleConnection();
                using var cmd = new OracleCommand();
                cmd.Connection = conn;
                cmd.CommandText = $"ALTER USER \"{request.Username}\" PROFILE \"{request.ProfileName.ToUpper()}\"";

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    message = $"Profile '{request.ProfileName}' has been assigned to user '{request.Username}'"
                });
            }
            catch (OracleException ex)
            {
                if (ex.Number == 2379) // ORA-02379: profile does not exist
                {
                    return NotFound(new
                    {
                        message = $"Profile '{request.ProfileName}' does not exist",
                        oracleError = ex.Number,
                        detail = ex.Message
                    });
                }

                if (ex.Number == 1918) // ORA-01918: user does not exist
                {
                    return NotFound(new
                    {
                        message = $"User '{request.Username}' does not exist",
                        oracleError = ex.Number,
                        detail = ex.Message
                    });
                }

                return StatusCode(500, new
                {
                    message = "Failed to assign profile",
                    oracleError = ex.Number,
                    detail = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Unexpected error",
                    detail = ex.Message
                });
            }
        }
    }
}
