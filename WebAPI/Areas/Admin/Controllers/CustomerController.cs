using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Models.Auth;
using WebAPI.Helpers;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public CustomerController(ControllerHelper helper)
        {
            _helper = helper;
        }

        // =====================
        // 🟢 GET ALL CUSTOMERS
        // =====================
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_ALL_CUSTOMERS",
                    "p_cursor",
                    reader => new
                    {
                        Phone = reader.GetStringSafe(0),
                        FullName = reader.GetStringSafe(1),
                        Email = reader.GetStringSafe(2),
                        Status = reader.GetStringSafe(3),
                        Roles = reader.GetStringSafe(4)
                    });

                return Ok(list);
            }, "Lỗi khi lấy danh sách khách hàng");
        }

        [HttpPost("unlock")]
        [Authorize]
        public IActionResult UnlockUser([FromBody] UnlockCustomerDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Phone))
                return BadRequest(new { message = "Username không hợp lệ." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                OracleHelper.ExecuteNonQuery(conn, "APP.UNLOCK_DB_USER",
                    ("p_username", OracleDbType.Varchar2, dto.Phone));

                return Ok(new { message = $"Tài khoản {dto.Phone} đã được unlock." });
            }, "Lỗi khi unlock tài khoản");
        }

        [HttpPost("lock")]
        [Authorize]
        public IActionResult LockUser([FromBody] UnlockCustomerDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Phone))
                return BadRequest(new { message = "Username không hợp lệ." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                OracleHelper.ExecuteNonQuery(conn, "APP.LOCK_DB_USER",
                    ("p_username", OracleDbType.Varchar2, dto.Phone));

                return Ok(new { message = $"Tài khoản {dto.Phone} đã bị khóa." });
            }, "Lỗi khi lock tài khoản");
        }
    }
}
