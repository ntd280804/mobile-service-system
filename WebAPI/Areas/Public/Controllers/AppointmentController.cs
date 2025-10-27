using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;

using WebAPI.Helpers;

using WebAPI.Services;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {

        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public AppointmentController(
                                  OracleConnectionManager connManager,
                                  JwtHelper jwtHelper,
                                  OracleSessionHelper oracleSessionHelper)
        {

            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
        }

        public class CreateAppointmentDto
        {
            public string CustomerPhone { get; set; }
            public DateTime AppointmentDate { get; set; }
            public string Description { get; set; }
        }
        public class AppointmentDto
        {
            public int AppointmentId { get; set; }
            public string CustomerPhone { get; set; }
            public DateTime AppointmentDate { get; set; }
            public string Status { get; set; }
            public string Description { get; set; }
        }
        [HttpGet("get-by-phone")]
        [Authorize]
        public IActionResult GetByPhone([FromQuery] string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return BadRequest(new { message = "Số điện thoại không được để trống" });

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_APPOINTMENTS_BY_PHONE", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_customer_phone", OracleDbType.Varchar2).Value = phone;

                var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(cursorParam);

                using var reader = cmd.ExecuteReader();

                var list = new List<AppointmentDto>();
                while (reader.Read())
                {
                    list.Add(new AppointmentDto
                    {
                        AppointmentId = reader.GetInt32(reader.GetOrdinal("APPOINTMENT_ID")),
                        CustomerPhone = reader.GetString(reader.GetOrdinal("CUSTOMER_PHONE")),
                        AppointmentDate = reader.GetDateTime(reader.GetOrdinal("APPOINTMENT_DATE")),
                        Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? null : reader.GetString(reader.GetOrdinal("STATUS")),
                        Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION"))
                    });
                }

                return Ok(list);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = "Lỗi Oracle", detail = ex.Message, number = ex.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpPost("create")]
        [Authorize]
        public IActionResult Create([FromBody] CreateAppointmentDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.CustomerPhone) || dto.AppointmentDate == default)
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.CREATE_APPOINTMENT", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_customer_phone", OracleDbType.Varchar2).Value = dto.CustomerPhone;
                cmd.Parameters.Add("p_appointment_date", OracleDbType.Date).Value = dto.AppointmentDate;
                cmd.Parameters.Add("p_description", OracleDbType.Varchar2).Value = dto.Description ?? (object)DBNull.Value;

                var outStatusParam = new OracleParameter("p_status", OracleDbType.Varchar2, 20)
                { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(outStatusParam);

                cmd.ExecuteNonQuery();

                var status = outStatusParam.Value?.ToString() ?? "SCHEDULED";

                return Ok(new
                {
                    message = "Đặt lịch thành công",
                    status
                });
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { message = "Lỗi Oracle", detail = ex.Message, number = ex.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

    }
}