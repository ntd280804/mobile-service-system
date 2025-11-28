using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Models.Order;
namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public OrderController(
            ControllerHelper helper)
        {
            _helper = helper;
        }
        [HttpGet("all")]
        [Authorize]
        public IActionResult GetByPhone()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
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
            }, "Lỗi khi lấy danh sách đơn hàng");
        }
        string SafeGetString(OracleDataReader r, int index)
        {
            return r.IsDBNull(index) ? "" : r.GetString(index);
        }

    }
}

