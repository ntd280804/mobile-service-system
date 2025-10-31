using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;

using WebAPI.Helpers;

using WebAPI.Services;
using WebAPI.Models.Appointment;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
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

        [HttpGet("all")]
        [Authorize]
        public IActionResult GetAll()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_APPOINTMENTS", conn);
                cmd.CommandType = CommandType.StoredProcedure;

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


    }
}