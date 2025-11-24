using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebAPI.Helpers;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private const string AdminRole = "ROLE_ADMIN";
        private const string CommonObjectFilter = "'EMPLOYEE','CUSTOMER','ORDERS','STOCK_IN','STOCK_IN_ITEM'," +
                                                  "'PART','STOCK_OUT','STOCK_OUT_ITEM','PART_REQUEST','PART_REQUEST_ITEM'," +
                                                  "'USER_OTP_LOG','EMPLOYEE_SHIFT','CUSTOMER_APPOINTMENT','INVOICE'," +
                                                  "'INVOICE_ITEM','SERVICE','ORDER_SERVICE','INVOICE_SERVICE'";

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
        [HttpGet("trigger")]
        [Authorize]
        public IActionResult GetAllAuditLog()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
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

        [HttpGet("standard")]
        [Authorize]
        public IActionResult GetStandardAudit()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_AUDIT_PROJECT";
                cmd.CommandType = CommandType.StoredProcedure;

                var outputCursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        EventTs = reader["EVENT_TS"] != DBNull.Value ? Convert.ToDateTime(reader["EVENT_TS"]) : (DateTime?)null,
                        DbUser = reader["DB_USER"]?.ToString(),
                        ObjectSchema = reader["OBJECT_SCHEMA"]?.ToString(),
                        ObjectName = reader["OBJECT_NAME"]?.ToString(),
                        Action = reader["ACTION"]?.ToString(),
                        Note = reader["NOTE"]?.ToString()
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
                return StatusCode(500, new { message = "Lỗi khi lấy standard audit", detail = ex.Message });
            }
        }

        [HttpGet("fga")]
        [Authorize]
        public IActionResult GetFgaAudit()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    SELECT TIMESTAMP AS EVENT_TS,
                           DB_USER,
                           OS_USER,
                           USERHOST,
                           OBJECT_SCHEMA,
                           OBJECT_NAME,
                           POLICY_NAME,
                           STATEMENT_TYPE,
                           SCN,
                           SQL_TEXT,
                           SQL_BIND
                    FROM DBA_FGA_AUDIT_TRAIL
                    WHERE OBJECT_NAME IN ({CommonObjectFilter})
                    ORDER BY TIMESTAMP DESC";
                cmd.CommandType = CommandType.Text;

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        EventTs = reader["EVENT_TS"] != DBNull.Value ? Convert.ToDateTime(reader["EVENT_TS"]) : (DateTime?)null,
                        DbUser = reader["DB_USER"]?.ToString(),
                        OsUser = reader["OS_USER"]?.ToString(),
                        UserHost = reader["USERHOST"]?.ToString(),
                        ObjectSchema = reader["OBJECT_SCHEMA"]?.ToString(),
                        ObjectName = reader["OBJECT_NAME"]?.ToString(),
                        PolicyName = reader["POLICY_NAME"]?.ToString(),
                        StatementType = reader["STATEMENT_TYPE"]?.ToString(),
                        Scn = reader["SCN"] != DBNull.Value ? Convert.ToInt64(reader["SCN"]) : (long?)null,
                        SqlText = reader["SQL_TEXT"]?.ToString(),
                        SqlBind = reader["SQL_BIND"]?.ToString()
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
                return StatusCode(500, new { message = "Lỗi khi lấy FGA audit", detail = ex.Message });
            }
        }

        [HttpPost("trigger/enable")]
        [Authorize]
        public IActionResult EnableTriggerAudit() => ExecuteAuditToggle("APP.TRIGGER_ENABLE_ALL", "Đã bật trigger audit.");

        [HttpPost("trigger/disable")]
        [Authorize]
        public IActionResult DisableTriggerAudit() => ExecuteAuditToggle("APP.TRIGGER_DISABLE_ALL", "Đã tắt trigger audit.");

        [HttpPost("standard/enable")]
        [Authorize]
        public IActionResult EnableStandardAudit() => ExecuteAuditToggle("APP.AUDIT_STD_ENABLE_ALL", "Đã bật standard audit.");

        [HttpPost("standard/disable")]
        [Authorize]
        public IActionResult DisableStandardAudit() => ExecuteAuditToggle("APP.AUDIT_STD_DISABLE_ALL", "Đã tắt standard audit.");

        [HttpPost("fga/enable")]
        [Authorize]
        public IActionResult EnableFgaAudit() => ExecuteAuditToggle("APP.ENABLE_ALL_FGA", "Đã bật FGA audit.");

        [HttpPost("fga/disable")]
        [Authorize]
        public IActionResult DisableFgaAudit() => ExecuteAuditToggle("APP.DISABLE_ALL_FGA", "Đã tắt FGA audit.");

        [HttpGet("status")]
        [Authorize]
        public IActionResult GetAuditStatus()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                // Check Standard Audit status
                int standardAuditCount = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
                        SELECT COUNT(*)
                        FROM DBA_OBJ_AUDIT_OPTS
                        WHERE object_name IN ({CommonObjectFilter})";
                    cmd.CommandType = CommandType.Text;
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        standardAuditCount = Convert.ToInt32(result);
                    }
                }

                // Check Trigger Audit status
                int triggerAuditCount = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_ENABLED_TRIGGERS_COUNT";
                    cmd.CommandType = CommandType.StoredProcedure;
                    var pCount = new OracleParameter("p_count", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pCount);
                    cmd.ExecuteNonQuery();
                    if (pCount.Value != DBNull.Value && pCount.Value is OracleDecimal oracleDecimal)
                        triggerAuditCount = oracleDecimal.ToInt32();
                    // Nếu dùng ExecuteScalar:
                    // var scalarResult = cmd.ExecuteScalar();
                    // if (scalarResult is OracleDecimal dec) n = dec.ToInt32();
                }

                // FGA - tạm để đó, trả về false
                bool fgaEnabled = false;

                return Ok(new
                {
                    StandardAudit = new
                    {
                        Enabled = standardAuditCount == 18,
                        Count = standardAuditCount,
                        ExpectedCount = 18
                    },
                    TriggerAudit = new
                    {
                        Enabled = triggerAuditCount == 12,
                        Count = triggerAuditCount,
                        ExpectedCount = 12
                    },
                    FgaAudit = new
                    {
                        Enabled = fgaEnabled,
                        Count = 0,
                        ExpectedCount = 0,
                        Note = "Tạm thời chưa kiểm tra"
                    }
                });
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy trạng thái audit", detail = ex.Message });
            }
        }

        private IActionResult ExecuteAuditToggle(string procedureName, string successMessage)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = procedureName;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.ExecuteNonQuery();

                return Ok(new { message = successMessage });
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi khi thực thi {procedureName}", detail = ex.Message });
            }
        }

        private bool TryEnsureAdminRole(OracleConnection conn, out IActionResult failureResult)
        {
            failureResult = null;

            string username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            if (string.IsNullOrEmpty(username))
            {
                failureResult = Unauthorized(new { message = "Missing Oracle username" });
                return false;
            }

            string roleList;
            using (var cmdRole = conn.CreateCommand())
            {
                cmdRole.CommandText = "SELECT APP.GET_EMPLOYEE_ROLES_BY_USERNAME(:p_username) FROM DUAL";
                cmdRole.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                var roleResult = cmdRole.ExecuteScalar();
                roleList = roleResult?.ToString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(roleList) || !roleList.Contains(AdminRole))
            {
                failureResult = Forbid("Only ROLE_ADMIN can access audit logs");
                return false;
            }

            return true;
        }
    }
}

