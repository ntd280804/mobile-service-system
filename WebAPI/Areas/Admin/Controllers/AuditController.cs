using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebAPI.Helpers;

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
                                                  "'INVOICE_ITEM','SERVICE','ORDER_SERVICE'";

        private readonly ControllerHelper _helper;

        public AuditController(ControllerHelper helper)
        {
            _helper = helper;
        }
        [HttpGet("trigger")]
        [Authorize]
        public async Task<IActionResult> GetAllAuditLog()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                var result = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_AUDIT_LOG", "p_cursor",
                    reader => new
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

                return Ok(result);
            }, "Lỗi khi lấy audit log");
        }

        [HttpGet("standard")]
        [Authorize]
        public async Task<IActionResult> GetStandardAudit()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                var result = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_AUDIT_PROJECT", "p_cursor",
                    reader => new
                    {
                        EventTs = reader["EVENT_TS"] != DBNull.Value ? Convert.ToDateTime(reader["EVENT_TS"]) : (DateTime?)null,
                        DbUser = reader["DB_USER"]?.ToString(),
                        ObjectSchema = reader["OBJECT_SCHEMA"]?.ToString(),
                        ObjectName = reader["OBJECT_NAME"]?.ToString(),
                        Action = reader["ACTION"]?.ToString(),
                        Note = reader["NOTE"]?.ToString()
                    });

                return Ok(result);
            }, "Lỗi khi lấy standard audit");
        }

        [HttpGet("fga")]
        [Authorize]
        public async Task<IActionResult> GetFgaAudit()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
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
            }, "Lỗi khi lấy FGA audit");
        }

        [HttpPost("trigger/enable")]
        [Authorize]
        public async Task<IActionResult> EnableTriggerAudit() => await ExecuteAuditToggle("APP.TRIGGER_ENABLE_ALL", "Đã bật trigger audit.");

        [HttpPost("trigger/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTriggerAudit() => await ExecuteAuditToggle("APP.TRIGGER_DISABLE_ALL", "Đã tắt trigger audit.");

        [HttpPost("standard/enable")]
        [Authorize]
        public async Task<IActionResult> EnableStandardAudit() => await ExecuteAuditToggle("APP.AUDIT_STD_ENABLE_ALL", "Đã bật standard audit.");

        [HttpPost("standard/disable")]
        [Authorize]
        public async Task<IActionResult> DisableStandardAudit() => await ExecuteAuditToggle("APP.AUDIT_STD_DISABLE_ALL", "Đã tắt standard audit.");

        [HttpPost("fga/enable")]
        [Authorize]
        public async Task<IActionResult> EnableFgaAudit() => await ExecuteAuditToggle("APP.ENABLE_ALL_FGA", "Đã bật FGA audit.");

        [HttpPost("fga/disable")]
        [Authorize]
        public async Task<IActionResult> DisableFgaAudit() => await ExecuteAuditToggle("APP.DISABLE_ALL_FGA", "Đã tắt FGA audit.");

        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> GetAuditStatus()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
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

                // Check Trigger Audit status (dùng helper để giữ nguyên procedure cũ)
                int triggerAuditCount = OracleHelper.ExecuteScalar<int>(
                    conn,
                    "APP.GET_ENABLED_TRIGGERS_COUNT",
                    "p_count");

                // FGA - tạm để đó, trả về false
                bool fgaEnabled = false;

                return Ok(new
                {
                    StandardAudit = new
                    {
                        Enabled = standardAuditCount == 17,
                        Count = standardAuditCount,
                        ExpectedCount = 17
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
            }, "Lỗi khi lấy trạng thái audit");
        }

        private async Task<IActionResult> ExecuteAuditToggle(string procedureName, string successMessage)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                if (!TryEnsureAdminRole(conn, out var failureResult))
                {
                    return failureResult;
                }

                // Gọi procedure cũ thông qua OracleHelper để chuẩn hoá
                OracleHelper.ExecuteNonQuery(conn, procedureName);

                return Ok(new { message = successMessage });
            }, $"Lỗi khi thực thi {procedureName}");
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

