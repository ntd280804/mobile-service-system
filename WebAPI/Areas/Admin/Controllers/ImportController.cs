using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;

        public ImportController(OracleConnectionManager connManager, JwtHelper jwtHelper)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
        }

        // GET: api/admin/import/getallimport
        [HttpGet("getallimport")]
        public IActionResult GetAllImports()
        {
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
                return Unauthorized(new { message = "Missing Oracle session headers" });

            try
            {
                var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_IMPORTS";
                cmd.CommandType = CommandType.StoredProcedure;

                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        StockInId = reader["STOCKIN_ID"],
                        EmpId = reader["EMP_ID"],
                        InDate = reader["IN_DATE"],
                        Note = reader["NOTE"],
                        StockInItem = new
                        {
                            StockInItemId = reader["STOCKIN_ITEM_ID"],
                            PartName = reader["PART_NAME"],
                            Manufacturer = reader["MANUFACTURER"],
                            Serial = reader["SERIAL"],
                            Status = reader["STATUS"]
                        }
                    });
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        // GET: api/admin/import/getimportid/5
        [HttpGet("getimportid/{id}")]
        public IActionResult GetImportById(int id)
        {
            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
                return Unauthorized(new { message = "Missing Oracle session headers" });

            try
            {
                var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_IMPORT_BY_ID";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = id;
                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var items = new List<object>();
                int stockInId = 0, empId = 0;
                DateTime inDate = DateTime.MinValue;
                string note = "";

                using var reader = cmd.ExecuteReader();
                if (!reader.HasRows) return NotFound("StockIn not found");

                while (reader.Read())
                {
                    stockInId = Convert.ToInt32(reader["STOCKIN_ID"]);
                    empId = Convert.ToInt32(reader["EMP_ID"]);
                    inDate = Convert.ToDateTime(reader["IN_DATE"]);
                    note = reader["NOTE"].ToString();

                    items.Add(new
                    {
                        StockInItemId = reader["STOCKIN_ITEM_ID"],
                        PartName = reader["PART_NAME"],
                        Manufacturer = reader["MANUFACTURER"],
                        Serial = reader["SERIAL"],
                        Status = reader["STATUS"]
                    });
                }

                return Ok(new
                {
                    StockInId = stockInId,
                    EmpId = empId,
                    InDate = inDate,
                    Note = note,
                    Items = items
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        // POST: api/admin/import/post
        [HttpPost("post")]
        public IActionResult ImportStockStepByStepWithTransaction([FromBody] ImportStockDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("No items to import");

            var username = HttpContext.Request.Headers["X-Oracle-Username"].FirstOrDefault();
            var platform = HttpContext.Request.Headers["X-Oracle-Platform"].FirstOrDefault();
            var sessionId = HttpContext.Request.Headers["X-Oracle-SessionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
                return Unauthorized(new { message = "Missing Oracle session headers" });

            try
            {
                var conn = _connManager.GetConnectionIfExists(username, platform, sessionId);

                // --- Bắt đầu transaction ---
                using var transaction = conn.BeginTransaction();

                int stockInId;

                // --- 1. CREATE_STOCKIN ---
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "APP.CREATE_STOCKIN";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = dto.EmpId;
                    cmd.Parameters.Add("p_note", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.Note ?? "";

                    var outStockInId = new OracleParameter("p_stockin_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(outStockInId);

                    cmd.ExecuteNonQuery();
                    stockInId = Convert.ToInt32(outStockInId.Value.ToString());
                }

                // --- 2. Duyệt từng item ---
                foreach (var item in dto.Items)
                {
                    string partName = item.PartName;
                    string manufacturer = item.Manufacturer ?? "";
                    string serial = item.Serial;
                    int stockInItemId;

                    // --- CREATE_STOCKIN_ITEM ---
                    using (var cmdItem = conn.CreateCommand())
                    {
                        cmdItem.Transaction = transaction;
                        cmdItem.CommandText = "APP.CREATE_STOCKIN_ITEM";
                        cmdItem.CommandType = CommandType.StoredProcedure;

                        cmdItem.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                        cmdItem.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = partName;
                        cmdItem.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = manufacturer;
                        cmdItem.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;

                        var outStockInItemId = new OracleParameter("p_stockin_item_id", OracleDbType.Int32, ParameterDirection.Output);
                        cmdItem.Parameters.Add(outStockInItemId);

                        cmdItem.ExecuteNonQuery();
                        stockInItemId = Convert.ToInt32(outStockInItemId.Value.ToString());
                    }

                    // --- CREATE_PART ---
                    using (var cmdPart = conn.CreateCommand())
                    {
                        cmdPart.Transaction = transaction;
                        cmdPart.CommandText = "APP.CREATE_PART";
                        cmdPart.CommandType = CommandType.StoredProcedure;

                        cmdPart.Parameters.Add("p_stockin_item_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInItemId;
                        cmdPart.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = partName;
                        cmdPart.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = manufacturer;
                        cmdPart.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;

                        cmdPart.ExecuteNonQuery();
                    }
                }

                // --- Commit transaction nếu tất cả thành công ---
                transaction.Commit();

                return Ok(new { Message = "Import successful", StockInId = stockInId });
            }
            catch (OracleException ex)
            {
                // Nếu có lỗi (ví dụ duplicate serial), rollback sẽ tự động
                return BadRequest(new
                {
                    Message = "Oracle Error - Transaction rolled back",
                    ErrorCode = ex.Number,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    Parameters = new
                    {
                        dto.EmpId,
                        dto.Note,
                        Items = dto.Items.Select(item => $"{item.PartName}|{item.Manufacturer}|{item.Serial}").ToArray()
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = "General Error - Transaction rolled back",
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

    }

    // DTOs
    public class ImportItemDto
    {
        public string PartName { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
    }

    public class ImportStockDto
    {
        public int EmpId { get; set; }
        public string Note { get; set; }
        public List<ImportItemDto> Items { get; set; }
    }
}
