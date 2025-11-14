using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public AuditController(
            OracleConnectionManager connManager,
            OracleSessionHelper oracleSessionHelper)
        {
            _connManager = connManager;
            _oracleSessionHelper = oracleSessionHelper;
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAllAuditLog()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                // Check if user has ROLE_ADMIN
                string username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { message = "Missing Oracle username" });
                }

                // Get user roles
                string roleList;
                using (var cmdRole = conn.CreateCommand())
                {
                    cmdRole.CommandText = "SELECT APP.GET_EMPLOYEE_ROLES_BY_USERNAME(:p_username) FROM DUAL";
                    cmdRole.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                    var roleResult = cmdRole.ExecuteScalar(); // Renamed variable to avoid conflict
                    roleList = roleResult?.ToString() ?? "";
                }

                // Check if user has ROLE_ADMIN
                if (string.IsNullOrEmpty(roleList) || !roleList.Contains("ROLE_ADMIN"))
                {
                    return Forbid("Only ROLE_ADMIN can access audit logs");
                }

                // Get audit log
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_AUDIT_LOG";
                cmd.CommandType = CommandType.StoredProcedure;

                var outputCursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        LogId = reader["LOG_ID"] != DBNull.Value ? Convert.ToInt64(reader["LOG_ID"]) : 0,
                        EventTs = reader["EVENT_TS"] != DBNull.Value ? Convert.ToDateTime(reader["EVENT_TS"]) : (DateTime?)null,
                        DbUser = reader["DB_USER"]?.ToString(),
                        OsUser = reader["OS_USER"]?.ToString(),
                        Machine = reader["MACHINE"]?.ToString(),
                        Module = reader["MODULE"]?.ToString(),
                        AppRole = reader["APP_ROLE"]?.ToString(),
                        EmpId = reader["EMP_ID"]?.ToString(),
                        CustomerPhone = reader["CUSTOMER_PHONE"]?.ToString(),
                        ObjectName = reader["OBJECT_NAME"]?.ToString(),
                        Note = reader["NOTE"]?.ToString(),
                        DmlType = reader["DML_TYPE"]?.ToString(),
                        ChangedColumns = reader["CHANGED_COLUMNS"]?.ToString(),
                        OldValues = reader["OLD_VALUES"]?.ToString(),
                        NewValues = reader["NEW_VALUES"]?.ToString()
                    });
                }

                return Ok(result);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy audit log", detail = ex.Message });
            }
        }
    }
}

