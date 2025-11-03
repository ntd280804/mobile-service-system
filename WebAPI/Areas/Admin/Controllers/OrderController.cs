using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;
using WebAPI.Models.Order;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
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

        [HttpGet]
        [Authorize]
        public IActionResult GetAllOrders()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_ORDERS", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Ref cursor output
                var cursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<OrderDto>();

                while (reader.Read())
                {
                    list.Add(new OrderDto
                    {
                        OrderId = reader.GetDecimal(0),
                        CustomerPhone = reader.GetString(1),
                        ReceiverEmpName = reader.GetString(2),
                        HandlerEmpName = reader.GetString(3),
                        OrderType = reader.GetString(4),
                        ReceivedDate = reader.GetDateTime(5),
                        Status = reader.GetString(6),
                        Description = reader.IsDBNull(7) ? "" : reader.GetString(7)
                        
                    });
                }

                return Ok(list);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                // Phiên Oracle bị kill
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách đơn hàng", detail = ex.Message });
            }
        }
        [HttpGet("by-order-type")]
        [Authorize]
        public IActionResult GetByOrderType([FromQuery] string orderType)
        {
            if (string.IsNullOrEmpty(orderType))
                return BadRequest(new { message = "orderType không được để trống." });

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_BY_ORDER_TYPE", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Tham số đầu vào
                cmd.Parameters.Add("p_order_type", OracleDbType.Varchar2, ParameterDirection.Input).Value = orderType;

                // Ref cursor output
                var cursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<OrderDto>();

                while (reader.Read())
                {
                    list.Add(new OrderDto
                    {
                        OrderId = reader.GetDecimal(0),
                        CustomerPhone = reader.GetString(1),
                        ReceiverEmpName = reader.GetString(2),
                        HandlerEmpName = reader.GetString(3),
                        OrderType = reader.GetString(4),
                        ReceivedDate = reader.GetDateTime(5),
                        Status = reader.GetString(6),
                        Description = reader.IsDBNull(7) ? "" : reader.GetString(7)

                    });
                }

                return Ok(list);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách đơn hàng theo OrderType", detail = ex.Message });
            }
        }
        [HttpPost]
        [Authorize]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.CREATE_ORDER", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // Tham số input
                cmd.Parameters.Add("p_customer_phone", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.CustomerPhone;
                cmd.Parameters.Add("p_receiver_emp_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.ReceiverEmpName;
                cmd.Parameters.Add("p_handler_emp_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.HandlerEmpName;
                cmd.Parameters.Add("p_order_type", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.OrderType;
                
                cmd.Parameters.Add("p_status", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.Status;
                cmd.Parameters.Add("p_description", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.Description ?? (object)DBNull.Value;

                cmd.ExecuteNonQuery();

                return Ok(new { message = "Tạo đơn hàng thành công." });
            }
            catch (OracleException ex)
            {
                if (ex.Number == 20001)
                    return BadRequest(new { message = ex.Message }); // username không tồn tại
                if (ex.Number == 28)
                {
                    _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                    _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                    return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
                }

                return StatusCode(500, new { message = "Lỗi khi tạo đơn hàng", detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tạo đơn hàng", detail = ex.Message });
            }
        }

        [HttpGet("details/{orderId}")]
        [Authorize]
        public IActionResult GetOrderDetails(int orderId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_ORDER_BY_ID", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = orderId;
                var cursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                    return NotFound(new { message = $"Order ID {orderId} not found" });

                OrderDto? order = null;
                if (reader.Read())
                {
                    order = new OrderDto
                    {
                        OrderId = reader.GetDecimal(0),
                        CustomerPhone = reader.GetString(1),
                        ReceiverEmpName = reader.GetString(2),
                        HandlerEmpName = reader.GetString(3),
                        OrderType = reader.GetString(4),
                        ReceivedDate = reader.GetDateTime(5),
                        Status = reader.GetString(6),
                        Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                    };
                }

                return Ok(order);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy chi tiết đơn hàng", detail = ex.Message });
            }
        }

        [HttpGet("customer-phones")]
        [Authorize]
        public IActionResult GetCustomerPhones()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_CUSTOMERS", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var phones = new List<string>();

                while (reader.Read())
                {
                    var phone = reader.GetString(0); // Phone is at index 0
                    phones.Add(phone);
                }

                return Ok(phones);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách số điện thoại khách hàng", detail = ex.Message });
            }
        }

        [HttpGet("handler-usernames")]
        [Authorize]
        public IActionResult GetHandlerUsernames()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_EMPLOYEES", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var usernames = new List<string>();

                while (reader.Read())
                {
                    var username = reader.GetString(2); // Username is at index 2
                    usernames.Add(username);
                }

                return Ok(usernames);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách username nhân viên", detail = ex.Message });
            }
        }

    }
}