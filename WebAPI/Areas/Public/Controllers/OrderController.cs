using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Helpers;
using WebAPI.Models.Appointment;
using WebAPI.Models.Order;
using WebAPI.Services;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;

        public OrderController(
            OracleConnectionManager connManager,
            JwtHelper jwtHelper,
            OracleSessionHelper oracleSessionHelper)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
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
                using var cmd = new OracleCommand("APP.GET_ALL_ORDERS", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                var cursorParam = new OracleParameter("cur_out", OracleDbType.RefCursor)
                { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(cursorParam);

                using var reader = cmd.ExecuteReader();

                var list = new List<OrderDto>();
                while (reader.Read())
                {
                    list.Add(new OrderDto
                    {
                        OrderId = reader.GetDecimal(0),
                        CustomerPhone = SafeGetString(reader, 1),
                        ReceiverEmpName = SafeGetString(reader, 2),
                        HandlerEmpName = SafeGetString(reader, 3),
                        OrderType = SafeGetString(reader, 4),
                        ReceivedDate = reader.GetDateTime(5),
                        Status = SafeGetString(reader, 6),
                        Description = SafeGetString(reader, 7)
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
        string SafeGetString(OracleDataReader r, int index)
        {
            return r.IsDBNull(index) ? "" : r.GetString(index);
        }

    }
}

