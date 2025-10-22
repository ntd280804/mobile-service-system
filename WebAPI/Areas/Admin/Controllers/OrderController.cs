using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;

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
        public class OrderDto
        {
            public decimal OrderId { get; set; }
            public string CustomerPhone { get; set; } = string.Empty;
            public string ReceiverEmpName { get; set; } = string.Empty;
            public string HandlerEmpName { get; set; } = string.Empty;
            public string OrderType { get; set; } = string.Empty;
            public DateTime ReceivedDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
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
        public class CreateOrderRequest
        {
            public string CustomerPhone { get; set; } = string.Empty;
            public string ReceiverEmpName { get; set; } = string.Empty;
            public string HandlerEmpName { get; set; } = string.Empty;
            public string OrderType { get; set; } = string.Empty;
            
            public string Status { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
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

    }
}