using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {

        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public CustomerController(
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper)
        {

            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
        }

        // =====================
        // 🟢 GET ALL EMPLOYEES
        // =====================
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_CUSTOMERS", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    list.Add(new
                    {
                        Phone = reader.GetString(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2),
                        Status = reader.GetString(3),
                        Roles = reader.IsDBNull(4) ? "" : reader.GetString(4) // Cột Roles
                    });
                }

                return Ok(list);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                // Retrieve session info from HttpContext
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách khách hàng", detail = ex.Message });
            }
        }
        [HttpPost("unlock")]
        [Authorize]
        public IActionResult UnlockUser([FromBody] UnlockCustomerDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Phone))
                return BadRequest(new { message = "Username không hợp lệ." });

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.UNLOCK_DB_USER", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Phone;

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Tài khoản {dto.Phone} đã được unlock." });
            }
            catch (OracleException ex)
            {
                return Conflict(new { message = $"Lỗi Oracle: {ex.Message}", number = ex.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        public class UnlockCustomerDto
        {
            public string Phone { get; set; }
        }

        [HttpPost("lock")]
        [Authorize]
        public IActionResult LockUser([FromBody] UnlockCustomerDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Phone))
                return BadRequest(new { message = "Username không hợp lệ." });

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.LOCK_DB_USER", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = dto.Phone;

                cmd.ExecuteNonQuery();

                return Ok(new { message = $"Tài khoản {dto.Phone} đã bị khóa." });
            }
            catch (OracleException ex)
            {
                return Conflict(new { message = $"Lỗi Oracle: {ex.Message}", number = ex.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}