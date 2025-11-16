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
    public class PartController : ControllerBase
    {

        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly QrGeneratorSingleton _qrGenerator;

        public PartController(OracleConnectionManager connManager, JwtHelper jwtHelper, OracleSessionHelper oracleSessionHelper,QrGeneratorSingleton _QR)
        {

            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
            _qrGenerator = _QR;
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_PART", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    // Lấy giá trị an toàn từ Reader, xử lý các cột có thể NULL
                    // Index 2: MANUFACTURER (VARCHAR2, Yes)
                    var manufacturer = reader.IsDBNull(2) ? null : reader.GetString(2);
                    // Index 6: ORDER_ID (NUMBER, Yes)
                    var orderId = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6);
                    // Index 7: PRICE (NUMBER(20,2), Yes)
                    var price = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7);

                    list.Add(new
                    {
                        PartId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Manufacturer = manufacturer, // Cột NULL
                        Serial = reader.GetString(3),
                        QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                        Status = reader.GetString(4),
                        StockinID = reader.GetDecimal(5),
                        OrderId = orderId, // Cột NULL
                        Price = price // Cột NULL (sử dụng GetDecimal cho kiểu NUMBER(20,2))
                    });
                }

                return Ok(list);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                // Retrieve session info from HttpContext
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                // Đã sửa thông báo lỗi từ 'nhân viên' sang 'linh kiện'
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách linh kiện", detail = ex.Message });
            }
        }

        [HttpGet("in-stock")]
        [Authorize]
        public IActionResult GetPartsInStock()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = new OracleCommand("APP.GET_ALL_PART_IN_STOCK", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var cursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    var manufacturer = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var orderId = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6);
                    var price = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7);

                    list.Add(new
                    {
                        PartId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Manufacturer = manufacturer,
                        Serial = reader.GetString(3),
                        QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                        Status = reader.GetString(4),
                        StockinID = reader.GetDecimal(5),
                        OrderId = orderId,
                        Price = price
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
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách linh kiện trong kho", detail = ex.Message });
            }
        }

        [HttpGet("detail-by-serial/{serial}")]
        [Authorize]
        public IActionResult GetPartBySerial(string serial)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_PART_BY_SERIAL";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;
                var outputCursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                using var reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                    return NotFound(new { message = $"Part with serial {serial} not found" });

                object? result = null;
                if (reader.Read())
                {
                    var manufacturer = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var orderId = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6);
                    var price = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7);

                    result = new
                    {
                        PartId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Manufacturer = manufacturer,
                        Serial = reader.GetString(3),
                        QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                        Status = reader.GetString(4),
                        StockinID = reader.GetDecimal(5),
                        OrderId = orderId,
                        Price = price
                    };
                }

                return Ok(result);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { Message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi lấy chi tiết linh kiện", Error = ex.Message });
            }
        }

        [HttpGet("details/{serial}")]
        [Authorize]
        public IActionResult GetPartDetails(string serial)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_PART_BY_SERIAL";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;
                var outputCursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                using var reader = cmd.ExecuteReader();
                if (!reader.HasRows)
                    return NotFound(new { message = $"serial ID {serial} not found" });

                object? result = null;
                if (reader.Read())
                {
                    var manufacturer = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var orderId = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6);
                    var price = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7);

                    result = new
                    {
                        PartId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Manufacturer = manufacturer,
                        Serial = reader.GetString(3),
                        QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                        Status = reader.GetString(4),
                        StockinID = reader.GetDecimal(5),
                        OrderId = orderId,
                        Price = price
                    };
                }

                return Ok(result);
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { Message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi lấy chi tiết linh kiện", Error = ex.Message });
            }
        }

        [HttpGet("by-order-id/{orderId}")]
        [Authorize]
        public IActionResult GetPartsByOrderId(int orderId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_PART_BY_ORDERID";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = orderId;
                var outputCursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    var manufacturer = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var orderIdValue = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6);
                    var price = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7);

                    list.Add(new
                    {
                        PartId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Manufacturer = manufacturer,
                        Serial = reader.GetString(3),
                        QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                        Status = reader.GetString(4),
                        StockinID = reader.GetDecimal(5),
                        OrderId = orderIdValue,
                        Price = price
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
            catch (OracleException ex)
            {
                return StatusCode(500, new { Message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi lấy danh sách linh kiện theo order", Error = ex.Message });
            }
        }

        [HttpGet("by-part-request/{orderId}")]
        [Authorize]
        public IActionResult GetPartsByPartRequest(int orderId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_PART_BY_PARTREQUEST";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = orderId;
                var outputCursor = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    var manufacturer = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var orderIdValue = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6);
                    var price = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7);
                    var requestId = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8);
                    var requestDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9);
                    var requestStatus = reader.IsDBNull(10) ? null : reader.GetString(10);

                    list.Add(new
                    {
                        PartId = reader.GetDecimal(0),
                        Name = reader.GetString(1),
                        Manufacturer = manufacturer,
                        Serial = reader.GetString(3),
                        QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                        Status = reader.GetString(4),
                        StockinID = reader.GetDecimal(5),
                        OrderId = orderIdValue,
                        Price = price,
                        RequestId = requestId,
                        RequestDate = requestDate,
                        RequestStatus = requestStatus
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
            catch (OracleException ex)
            {
                return StatusCode(500, new { Message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi lấy danh sách linh kiện từ yêu cầu", Error = ex.Message });
            }
        }


    }
}
