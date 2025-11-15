using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class BackupRestoreController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public BackupRestoreController(
            OracleConnectionManager connManager,
            OracleSessionHelper oracleSessionHelper)
        {
            _connManager = connManager;
            _oracleSessionHelper = oracleSessionHelper;
        }

        /// <summary>
        /// Chạy RMAN Backup job
        /// </summary>
        [HttpPost("backup")]
        [Authorize]
        public IActionResult RunBackup()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                string username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(ApiResponse<string>.Fail("Missing Oracle username"));
                }
                // Chạy job backup
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "BEGIN DBMS_SCHEDULER.RUN_JOB('APP.RUN_RMAN_PDB_BACKUP'); END;";
                cmd.ExecuteNonQuery();

                return Ok(ApiResponse<string>.Ok("Backup job đã được khởi chạy thành công"));
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(ApiResponse<string>.Fail("Phiên Oracle đã bị kill. Vui lòng đăng nhập lại."));
            }
            catch (OracleException ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Oracle error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Lỗi khi chạy backup: {ex.Message}"));
            }
        }

        /// <summary>
        /// Chạy RMAN Restore job
        /// </summary>
        [HttpPost("restore")]
        [Authorize]
        public IActionResult RunRestore()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                string username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(ApiResponse<string>.Fail("Missing Oracle username"));
                }

                // Chạy job restore
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "BEGIN DBMS_SCHEDULER.RUN_JOB('APP.RUN_RMAN_PDB_RESTORE'); END;";
                cmd.ExecuteNonQuery();

                return Ok(ApiResponse<string>.Ok("Restore job đã được khởi chạy thành công"));
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(ApiResponse<string>.Fail("Phiên Oracle đã bị kill. Vui lòng đăng nhập lại."));
            }
            catch (OracleException ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Oracle error {ex.Number}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"Lỗi khi chạy restore: {ex.Message}"));
            }
        }
    }
}

