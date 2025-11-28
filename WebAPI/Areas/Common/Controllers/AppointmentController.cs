using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Models.Appointment;
using System.Data;
namespace WebAPI.Areas.Common.Controllers
{
    [Area("Common")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public AppointmentController(ControllerHelper helper)
        {
            _helper = helper;
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_ALL_APPOINTMENTS",
                    "p_cursor",
                    reader => new AppointmentDto
                    {
                        AppointmentId = reader.GetInt32(reader.GetOrdinal("APPOINTMENT_ID")),
                        CustomerPhone = reader.GetString(reader.GetOrdinal("CUSTOMER_PHONE")),
                        AppointmentDate = reader.GetDateTime(reader.GetOrdinal("APPOINTMENT_DATE")),
                        Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? null : reader.GetString(reader.GetOrdinal("STATUS")),
                        Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION"))
                    });

                return Ok(list);
            }, "Lỗi khi lấy danh sách lịch hẹn");
        }
    }
}