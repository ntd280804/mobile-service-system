using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Models.Appointment;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public AppointmentController(ControllerHelper helper)
        {
            _helper = helper;
        }

        [HttpGet("all")]
        [Authorize]
        public IActionResult GetAll()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_APPOINTMENTS", "p_cursor",
                    reader => new AppointmentDto
                    {
                        AppointmentId = reader.GetInt32(reader.GetOrdinal("APPOINTMENT_ID")),
                        CustomerPhone = reader.GetString(reader.GetOrdinal("CUSTOMER_PHONE")),
                        AppointmentDate = reader.GetDateTime(reader.GetOrdinal("APPOINTMENT_DATE")),
                        Status = reader.GetStringSafe("STATUS"),
                        Description = reader.GetStringSafe("DESCRIPTION")
                    });
                return Ok(list);
            }, "Lỗi khi lấy danh sách lịch hẹn");
            }
        }
}
