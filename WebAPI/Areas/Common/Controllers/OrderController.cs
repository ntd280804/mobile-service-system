using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Models.Order;
namespace WebAPI.Areas.Common.Controllers
{
    [Area("Common")]
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
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Getall()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_ALL_ORDERS",
                    "cur_out",
                    reader => new OrderDto
                    {
                        OrderId = reader.GetDecimal(0),
                        CustomerPhone = reader.GetStringSafe(1),
                        ReceiverEmpName = reader.GetStringSafe(2),
                        HandlerEmpName = reader.GetStringSafe(3),
                        OrderType = reader.GetStringSafe(4),
                        ReceivedDate = reader.GetDateTime(5),
                        Status = reader.GetStringSafe(6),
                        Description = reader.GetStringSafe(7)
                    });

                return Ok(list);
            }, "Lỗi khi lấy danh sách đơn hàng");
        }

        [HttpGet("{orderId}/details")]
        [Authorize]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ORDER_BY_ID", "cur_out",
                    reader => new OrderDto
                    {
                        OrderId = reader.GetDecimal(0),
                        CustomerPhone = reader.GetString(1),
                        ReceiverEmpName = reader.GetString(2),
                        HandlerEmpName = reader.GetString(3),
                        OrderType = reader.GetString(4),
                        ReceivedDate = reader.GetDateTime(5),
                        Status = reader.GetString(6),
                        Description = reader.GetStringSafe(7)
                    },
                    ("p_order_id", OracleDbType.Int32, orderId));

                if (list.Count == 0)
                    return NotFound(new { message = $"Order ID {orderId} not found" });

                return Ok(list[0]);
            }, "Lỗi khi lấy chi tiết đơn hàng");
        }
    }
}


