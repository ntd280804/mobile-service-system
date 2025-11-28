using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Models.Appointment;
using System.Data;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public AppointmentController(ControllerHelper helper)
        {
            _helper = helper;
        }
        [HttpPost]
        [Authorize]
        public IActionResult Create([FromBody] CreateAppointmentDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.CustomerPhone) || dto.AppointmentDate == default)
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var outputs = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.CREATE_APPOINTMENT",
                    new[]
                    {
                        ("p_customer_phone", OracleDbType.Varchar2, (object?)dto.CustomerPhone ?? ""),
                        ("p_appointment_date", OracleDbType.Date, dto.AppointmentDate),
                        ("p_description", OracleDbType.Varchar2, dto.Description ?? (object)DBNull.Value)
                    },
                    new[] { ("p_status", OracleDbType.Varchar2) });

                var status = outputs["p_status"]?.ToString() ?? "SCHEDULED";

                return Ok(new
                {
                    message = "Đặt lịch thành công",
                    status
                });
            }, "Lỗi khi tạo lịch hẹn");
        }

    }
}