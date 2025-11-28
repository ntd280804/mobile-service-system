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
        public IActionResult Getall()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
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
    }
}


