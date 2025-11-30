using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Models.Order;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public OrderController(ControllerHelper helper)
        {
            _helper = helper;
        }

        [HttpGet("services")]
        [Authorize]
        public async Task<IActionResult> GetServices()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_SERVICES", "p_service_cursor",
                    reader => new ServiceDto
                    {
                        ServiceId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Description = reader.GetStringSafe(2),
                        Price = reader.GetDecimal(3)
                    });
                return Ok(list);
            }, "Lỗi khi lấy danh sách dịch vụ");
            }
        [HttpGet("by-order-type")]
        [Authorize]
        public async Task<IActionResult> GetByOrderType([FromQuery] string orderType)
        {
            if (string.IsNullOrEmpty(orderType))
                return BadRequest(new { message = "orderType không được để trống." });

            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_BY_ORDER_TYPE", "cur_out",
                    reader => MapOrder(reader),
                    ("p_order_type", OracleDbType.Varchar2, orderType));
                return Ok(list);
            }, "Lỗi khi lấy danh sách đơn hàng theo OrderType");
            }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            return await _helper.ExecuteWithTransaction(HttpContext, (conn, transaction) =>
            {
                // 1. Tạo đơn hàng và lấy ORDER_ID
                var orderId = OracleHelper.ExecuteScalar<int>(conn, "APP.CREATE_ORDER", "p_order_id", transaction,
                    ("p_customer_phone", OracleDbType.Varchar2, request.CustomerPhone),
                    ("p_receiver_emp_name", OracleDbType.Varchar2, request.ReceiverEmpName),
                    ("p_handler_emp_name", OracleDbType.Varchar2, request.HandlerEmpName),
                    ("p_order_type", OracleDbType.Varchar2, request.OrderType),
                    ("p_status", OracleDbType.Varchar2, request.Status),
                    ("p_description", OracleDbType.Varchar2, request.Description ?? (object)DBNull.Value));

                // 2. Tạo các ORDER_SERVICE
                if (request.ServiceItems != null)
                {
                    foreach (var item in request.ServiceItems)
                    {
                        var result = OracleHelper.ExecuteScalar<string>(conn, "APP.CREATE_ORDER_SERVICE", "p_result", transaction,
                            ("p_order_id", OracleDbType.Int32, orderId),
                            ("p_service_id", OracleDbType.Int32, item.ServiceId),
                            ("p_quantity", OracleDbType.Int32, item.Quantity),
                            ("p_price", OracleDbType.Decimal, item.Price));

                            if (!string.IsNullOrEmpty(result) && result.StartsWith("Lỗi:"))
                            throw new InvalidOperationException(result);
                            }
                        }

                return Ok(new { message = "Tạo đơn hàng thành công.", orderId });
            }, "Lỗi khi tạo đơn hàng");
        }

        [HttpGet("{orderId}/details")]
        [Authorize]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ORDER_BY_ID", "cur_out",
                    reader => MapOrder(reader),
                    ("p_order_id", OracleDbType.Int32, orderId));

                if (list.Count == 0)
                    return NotFound(new { message = $"Order ID {orderId} not found" });

                return Ok(list[0]);
            }, "Lỗi khi lấy chi tiết đơn hàng");
        }

        [HttpGet("{orderId}/services")]
        [Authorize]
        public async Task<IActionResult> GetOrderServices(int orderId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var services = OracleHelper.ExecuteRefCursor(conn, "APP.GET_SERVICES_BY_ORDER_ID", "p_cursor",
                    reader => new OrderServiceDto
                    {
                        ServiceId = reader.GetDecimal(0),
                        ServiceName = reader.GetString(1),
                        ServiceDescription = reader.GetStringSafe(2),
                        Quantity = reader.GetDecimal(3),
                        Price = reader.GetDecimal(4)
                    },
                    ("p_order_id", OracleDbType.Int32, orderId));
                return Ok(services);
            }, "Lỗi khi lấy danh sách dịch vụ của đơn hàng");
        }

        [HttpGet("customer-phones")]
        [Authorize]
        public async Task<IActionResult> GetCustomerPhones()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var phones = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_CUSTOMERS", "p_cursor",
                    reader => reader.GetString(0));
                return Ok(phones);
            }, "Lỗi khi lấy danh sách số điện thoại khách hàng");
        }

        [HttpGet("handler-usernames")]
        [Authorize]
        public async Task<IActionResult> GetHandlerUsernames()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var usernames = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_EMPLOYEES", "p_cursor",
                    reader => reader.GetString(2));
                return Ok(usernames);
            }, "Lỗi khi lấy danh sách username nhân viên");
            }

        [HttpPost("{orderId}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var result = OracleHelper.ExecuteScalar<string>(
                    conn,
                    "APP.CANCEL_ORDER",
                    "p_result",
                    null,
                    ("p_order_id", OracleDbType.Int32, orderId));

                if (string.IsNullOrEmpty(result))
                    return BadRequest(new { message = "Không nhận được kết quả từ procedure" });

                if (result.Contains("Lỗi") || result.Contains("không tồn tại") || result.Contains("đã được hủy"))
                    return BadRequest(new { message = result });

                return Ok(new { message = result });
            }, "Lỗi khi hủy đơn hàng");
        }

        // ============ Private Helpers ============

        private static OrderDto MapOrder(OracleDataReader reader)
        {
            return new OrderDto
            {
                OrderId = reader.GetDecimal(0),
                CustomerPhone = reader.GetString(1),
                ReceiverEmpName = reader.GetString(2),
                HandlerEmpName = reader.GetString(3),
                OrderType = reader.GetString(4),
                ReceivedDate = reader.GetDateTime(5),
                Status = reader.GetString(6),
                Description = reader.GetStringSafe(7)
            };
        }
    }
}
