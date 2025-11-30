using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using WebAPI.Helpers;
using WebAPI.Services;
using WebAPI.Models.Part;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class PartrequestController : ControllerBase
    {
        private readonly ControllerHelper _helper;
        private readonly QrGeneratorSingleton _qrGenerator;

        public PartrequestController(ControllerHelper helper, QrGeneratorSingleton qrGenerator)
        {
            _helper = helper;
            _qrGenerator = qrGenerator;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllImports()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var result = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_PART_REQUESTS", "cur_out",
                    reader => new
                    {
                        REQUEST_ID = reader["REQUEST_ID"],
                        ORDER_ID = reader["ORDER_ID"],
                        EmpUsername = reader["EmpUsername"],
                        REQUEST_DATE = reader["REQUEST_DATE"],
                        STATUS = reader["STATUS"]
                    });
                return Ok(result);
            }, "Internal Server Error");
        }

        [HttpPost("{requestId}/accept")]
        [Authorize]
        public async Task<IActionResult> AcceptPartRequest(int requestId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                OracleHelper.ExecuteNonQuery(conn, "APP.ACCEPT_PART_REQUEST",
                    ("p_request_id", OracleDbType.Int32, requestId));
                return Ok(new { message = "Accepted" });
            }, "Internal Server Error");
        }

        [HttpPost("{requestId}/deny")]
        [Authorize]
        public async Task<IActionResult> DenyPartRequest(int requestId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                OracleHelper.ExecuteNonQuery(conn, "APP.DENY_PART_REQUEST",
                    ("p_request_id", OracleDbType.Int32, requestId));
                return Ok(new { message = "Denied" });
            }, "Internal Server Error");
        }

        [HttpGet("{requestId}/by-request-id")]
        [Authorize]
        public async Task<IActionResult> GetPartsByRequestId(int requestId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var list = OracleHelper.ExecuteRefCursor(conn, "APP.GET_PART_BY_REQUEST_ID", "p_cursor",
                    reader => new
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
                    },
                    ("p_request_id", OracleDbType.Int32, requestId));
                return Ok(list);
            }, "Lỗi khi lấy danh sách linh kiện từ yêu cầu");
        }

        [HttpPost("post")]
        [Authorize]
        public async Task<IActionResult> CreatePartRequest([FromBody] CreatePartRequestDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("No items to request");
            if (string.IsNullOrEmpty(dto.EmpUsername))
                return BadRequest("Missing employee username");

            return await _helper.ExecuteWithTransaction(HttpContext, (conn, transaction) =>
            {
                // 1. Get EMP_ID from username
                var empId = OracleHelper.ExecuteScalar<int>(conn, "APP.GET_EMPLOYEE_ID_BY_USERNAME", "p_emp_id", transaction,
                    ("p_username", OracleDbType.Varchar2, dto.EmpUsername));

                // 2. Create PART_REQUEST
                var requestId = OracleHelper.ExecuteScalar<int>(conn, "APP.CREATE_PART_REQUEST", "p_request_id", transaction,
                    ("p_order_id", OracleDbType.Int32, (int)dto.OrderId),
                    ("p_emp_id", OracleDbType.Int32, empId),
                    ("p_status", OracleDbType.Varchar2, dto.Status),
                    ("p_request_date", OracleDbType.Date, dto.RequestDate));

                // 3. Create PART_REQUEST_ITEM for each item
                foreach (var item in dto.Items)
                {
                    OracleHelper.ExecuteNonQueryWithTransaction(conn, "APP.CREATE_PART_REQUEST_ITEM", transaction,
                        ("p_request_id", OracleDbType.Int32, requestId),
                        ("p_part_id", OracleDbType.Int32, item.PartId));
                }

                return Ok(new { message = "Part request created successfully", requestId });
            }, "Internal Server Error");
        }
    }
}
