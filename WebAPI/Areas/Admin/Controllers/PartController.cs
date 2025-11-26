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
        public IActionResult GetAll()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_PART", "p_cursor",
                    reader => MapPart(reader));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện");
        }

        [HttpGet("in-stock")]
        [Authorize]
        public IActionResult GetPartsInStock()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_PART_IN_STOCK", "p_cursor",
                    reader => MapPart(reader));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện trong kho");
        }

        [HttpGet("detail-by-serial/{serial}")]
        [Authorize]
        public IActionResult GetPartBySerial(string serial)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_SERIAL", "p_cursor",
                    reader => MapPart(reader),
                    ("p_serial", OracleDbType.Varchar2, serial));

                if (list.Count == 0)
                    return NotFound(new { message = $"Part with serial {serial} not found" });

                return Ok(list[0]);
            }, "Lỗi khi lấy chi tiết linh kiện");
        }

        [HttpGet("details/{serial}")]
        [Authorize]
        public IActionResult GetPartDetails(string serial)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_SERIAL", "p_cursor",
                    reader => MapPart(reader),
                    ("p_serial", OracleDbType.Varchar2, serial));

                if (list.Count == 0)
                    return NotFound(new { message = $"serial ID {serial} not found" });

                return Ok(list[0]);
            }, "Lỗi khi lấy chi tiết linh kiện");
        }

        [HttpGet("by-order-id/{orderId}")]
        [Authorize]
        public IActionResult GetPartsByOrderId(int orderId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_ORDERID", "p_cursor",
                    reader => MapPart(reader),
                    ("p_order_id", OracleDbType.Int32, orderId));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện theo order");
        }

        [HttpGet("by-part-request/{orderId}")]
        [Authorize]
        public IActionResult GetPartsByPartRequest(int orderId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_PARTREQUEST", "p_cursor",
                    reader => MapPartWithRequest(reader),
                    ("p_order_id", OracleDbType.Int32, orderId));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện từ yêu cầu");
        }

        // ============ Private Helpers ============

        /// <summary>
        /// Map a Part row from OracleDataReader (columns 0-7)
        /// </summary>
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

        /// <summary>
        /// Map a Part row with PartRequest info (columns 0-10)
        /// </summary>
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
