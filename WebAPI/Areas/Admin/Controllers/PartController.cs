using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class PartController : ControllerBase
    {
        private readonly ControllerHelper _helper;
        private readonly QrGeneratorSingleton _qrGenerator;

        public PartController(ControllerHelper helper, QrGeneratorSingleton qrGenerator)
        {
            _helper = helper;
            _qrGenerator = qrGenerator;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_PART", "p_cursor",
                    reader => MapPart(reader));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện");
        }

        [HttpGet("in-stock")]
        [Authorize]
        public async Task<IActionResult> GetPartsInStock()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_PART_IN_STOCK", "p_cursor",
                    reader => MapPart(reader));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện trong kho");
        }

        [HttpGet("{serial}/details")]
        [Authorize]
        public async Task<IActionResult> GetPartBySerial(string serial)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_SERIAL", "p_cursor",
                    reader => MapPart(reader),
                    ("p_serial", OracleDbType.Varchar2, serial));

                if (list.Count == 0)
                    return NotFound(new { message = $"Part with serial {serial} not found" });

                return Ok(list[0]);
            }, "Lỗi khi lấy chi tiết linh kiện");
        }

        [HttpGet("{orderId}/by-order-id")]
        [Authorize]
        public async Task<IActionResult> GetPartsByOrderId(int orderId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_ORDERID", "p_cursor",
                    reader => MapPart(reader),
                    ("p_order_id", OracleDbType.Int32, orderId));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện theo order");
        }

        [HttpGet("{orderId}/by-part-request")]
        [Authorize]
        public async Task<IActionResult> GetPartsByPartRequest(int orderId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_PARTREQUEST", "p_cursor",
                    reader => MapPartWithRequest(reader),
                    ("p_order_id", OracleDbType.Int32, orderId));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện từ yêu cầu");
        }

        private object MapPart(OracleDataReader reader)
        {
            return new
            {
                PartId = reader.GetDecimal(0),
                Name = reader.GetString(1),
                Manufacturer = reader.GetStringSafe(2),
                Serial = reader.GetString(3),
                QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                Status = reader.GetString(4),
                StockinID = reader.GetDecimal(5),
                OrderId = reader.GetDecimalOrNull(6),
                Price = reader.GetDecimalOrNull(7)
            };
        }
        private object MapPartWithRequest(OracleDataReader reader)
        {
            return new
            {
                PartId = reader.GetDecimal(0),
                Name = reader.GetString(1),
                Manufacturer = reader.GetStringSafe(2),
                Serial = reader.GetString(3),
                QRImage = _qrGenerator.GenerateQRImage(reader.GetString(3)),
                Status = reader.GetString(4),
                StockinID = reader.GetDecimal(5),
                OrderId = reader.GetDecimalOrNull(6),
                Price = reader.GetDecimalOrNull(7),
                RequestId = reader.GetDecimalOrNull(8),
                RequestDate = reader.GetDateTimeOrNull(9),
                RequestStatus = reader.GetStringSafe(10)
            };
        }
    }
}
