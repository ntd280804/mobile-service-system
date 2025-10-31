using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using WebAPI.Helpers;

using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly InvoicePdfService _invoicePdfService;

        public ImportController(OracleConnectionManager connManager, JwtHelper jwtHelper ,  OracleSessionHelper oracleSessionHelper, InvoicePdfService invoicePdfService)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
            _invoicePdfService = invoicePdfService;
        }

        // GET: api/admin/import/getallimport
        [HttpGet("getallimport")]
        [Authorize]
        public IActionResult GetAllImports()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
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
                        EmpUsername = reader["EmpUsername"],
                        InDate = reader["IN_DATE"],
                        Note = reader["NOTE"]
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpGet("invoice/{stockinId}")]
        [Authorize]
        public IActionResult GetImportInvoicePdf(int stockinId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                // 1) Load import header + items
                var items = new List<ImportItemDto>();
                string? empUsername = null;
                DateTime? inDate = null;
                string note = null;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_IMPORT_BY_ID";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                    var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                    cmd.Parameters.Add(outputCursor);

                    using var reader = cmd.ExecuteReader();
                    if (!reader.HasRows)
                        return NotFound(new { message = $"StockIn ID {stockinId} not found" });

                    while (reader.Read())
                    {
                        if (empUsername == null)
                        {
                            empUsername = Convert.ToString(reader["EmpUsername"]);
                            inDate = Convert.ToDateTime(reader["InDate"]);
                            note = reader["Note"]?.ToString();
                        }

                        items.Add(new ImportItemDto
                        {
                            PartName = reader["PartName"]?.ToString(),
                            Manufacturer = reader["Manufacturer"]?.ToString(),
                            Serial = reader["Serial"]?.ToString(),
                            Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                        });
                    }
                }

                var dto = new ImportStockDto
                {
                    StockInId = stockinId,
                    EmpUsername = empUsername ?? string.Empty,
                    InDate = inDate ?? DateTime.MinValue,
                    Note = note,
                    Items = items
                };

                // 2) Load persisted signature
                string signature;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_STOCKIN_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                    var pSig = new OracleParameter("p_signature", OracleDbType.Clob, ParameterDirection.Output);
                    cmd.Parameters.Add(pSig);
                    cmd.ExecuteNonQuery();

                    if (pSig.Value == DBNull.Value || pSig.Value == null)
                        signature = string.Empty;
                    else
                        signature = ((Oracle.ManagedDataAccess.Types.OracleClob)pSig.Value).Value;
                }

                // 3) Generate PDF without verify URL
                var pdfBytes = _invoicePdfService.GenerateImportInvoicePdf(dto, signature ?? string.Empty, null, null);

                return File(pdfBytes, "application/pdf", $"ImportInvoice_{stockinId}.pdf");
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { Message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "General Error", Error = ex.Message });
            }
        }


        [HttpGet("details/{stockinId}")]
        [Authorize]
        public IActionResult GetImportDetails(int stockinId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_IMPORT_BY_ID";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var items = new List<ImportItemDto>();
                string? empUsername = null;
                DateTime? inDate = null;
                string note = null;

                using var reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                    return NotFound(new { message = $"StockIn ID {stockinId} not found" });

                while (reader.Read())
                {
                    // Thông tin chung STOCK_IN (lấy 1 lần)
                    if (empUsername == null)
                    {
                        empUsername = Convert.ToString(reader["EmpUsername"]);
                        inDate = Convert.ToDateTime(reader["InDate"]);
                        note = reader["Note"]?.ToString();
                    }

                    // Chi tiết từng item
                    items.Add(new ImportItemDto
                    {
                        PartName = reader["PartName"]?.ToString(),
                        Manufacturer = reader["Manufacturer"]?.ToString(),
                        Serial = reader["Serial"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                    });
                }

                var result = new ImportStockDto
                {
                    StockInId = stockinId,
                    EmpUsername = empUsername ?? "",
                    InDate = inDate ?? DateTime.MinValue,
                    Note = note,
                    Items = items
                };

                return Ok(result);
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Oracle Error",
                    ErrorCode = ex.Number,
                    Error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "General Error",
                    Error = ex.Message
                });
            }
        }




        [HttpPost("post")]
        [Authorize]
        public IActionResult ImportStockStepByStepWithTransaction([FromBody] ImportStockDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("No items to import");
            if (string.IsNullOrEmpty(dto.PrivateKey))
                return BadRequest("Missing private key");

            var privateKeyPem = dto.PrivateKey;
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;
            try
            {

                // --- Bắt đầu transaction ---
                using var transaction = conn.BeginTransaction();

                int stockInId;
                string signatureBase64 = "DUMMY_SIGNATURE_FOR_DEMO_PURPOSES";

                int empId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EMPLOYEE_ID_BY_USERNAME";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.EmpUsername;
                    var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pEmpId);

                    cmd.ExecuteNonQuery();
                    empId = Convert.ToInt32(pEmpId.Value.ToString());
                }
                // --- 2. CREATE_STOCKIN với signature ---
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "APP.CREATE_STOCKIN";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                    cmd.Parameters.Add("p_note", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.Note ?? "";
                    cmd.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = signatureBase64;

                    var outStockInId = new OracleParameter("p_stockin_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(outStockInId);

                    cmd.ExecuteNonQuery();
                    stockInId = Convert.ToInt32(outStockInId.Value.ToString());
                }

                // --- 3. Duyệt từng item ---
                foreach (var item in dto.Items)
                {
                    string partName = item.PartName;
                    string manufacturer = item.Manufacturer ?? "";
                    string serial = item.Serial;
                    int stockInItemId;

                    using (var cmdItem = conn.CreateCommand())
                    {
                        cmdItem.Transaction = transaction;
                        cmdItem.CommandText = "APP.CREATE_STOCKIN_ITEM";
                        cmdItem.CommandType = CommandType.StoredProcedure;

                        cmdItem.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                        cmdItem.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = partName;
                        cmdItem.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = manufacturer;
                        cmdItem.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;
                        cmdItem.Parameters.Add("p_price", OracleDbType.Decimal, ParameterDirection.Input).Value = item.Price; // <-- thêm price


                        var outStockInItemId = new OracleParameter("p_stockin_item_id", OracleDbType.Int32, ParameterDirection.Output);
                        cmdItem.Parameters.Add(outStockInItemId);

                        cmdItem.ExecuteNonQuery();
                        stockInItemId = Convert.ToInt32(outStockInItemId.Value.ToString());
                    }

                    using (var cmdPart = conn.CreateCommand())
                    {
                        cmdPart.Transaction = transaction;
                        cmdPart.CommandText = "APP.CREATE_PART";
                        cmdPart.CommandType = CommandType.StoredProcedure;
                        cmdPart.Parameters.Add("p_stockin_item_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInItemId;
                        cmdPart.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = partName;
                        cmdPart.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = manufacturer;
                        cmdPart.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;
                        cmdPart.Parameters.Add("p_price", OracleDbType.Decimal, ParameterDirection.Input).Value = item.Price; // <-- thêm price


                        cmdPart.ExecuteNonQuery();
                    }
                }
                string signature;
                using (var cmdSign = conn.CreateCommand())
                {
                    cmdSign.Transaction = transaction;
                    cmdSign.CommandText = "APP.SIGN_STOCKIN";
                    cmdSign.CommandType = CommandType.StoredProcedure;

                    cmdSign.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                    cmdSign.Parameters.Add("p_private_key", OracleDbType.Varchar2, ParameterDirection.Input).Value = privateKeyPem;

                    var outSignature = new OracleParameter("p_signature", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                    cmdSign.Parameters.Add(outSignature);

                    cmdSign.ExecuteNonQuery();
                    signature = outSignature.Value.ToString();
                }

                // --- 5. Update signature vào bảng STOCK_IN ---
                using (var cmdUpdateSig = conn.CreateCommand())
                {
                    cmdUpdateSig.Transaction = transaction;
                    cmdUpdateSig.CommandText = "APP.UPDATE_STOCKIN_SIGNATURE";
                    cmdUpdateSig.CommandType = CommandType.StoredProcedure;

                    cmdUpdateSig.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                    cmdUpdateSig.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = signature;

                    cmdUpdateSig.ExecuteNonQuery();
                }
                transaction.Commit();
                return Ok(new { Message = "Import successful", StockInId = stockInId });
            }
            catch (OracleException ex)
            {
                return BadRequest(new
                {
                    Message = "Oracle Error - Transaction rolled back",
                    ErrorCode = ex.Number,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    Parameters = new
                    {
                        dto.EmpUsername,
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
        [HttpGet("verifysign/{stockinId}")]
        [Authorize]
        public IActionResult VerifyStockInSignature(int stockinId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {

                // --- 1. GET_EMP_ID_FROM_STOCKIN ---
                int empId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EMP_ID_FROM_STOCKIN";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                    var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pEmpId);

                    cmd.ExecuteNonQuery();

                    if (pEmpId.Value == DBNull.Value || pEmpId.Value == null)
                        return NotFound(new { message = $"StockIn ID {stockinId} không tồn tại" });

                    empId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)pEmpId.Value).ToInt32();
                }

                // --- 2. GET_EMPLOYEE_PUBLIC_KEY ---
                string publicKey;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EMPLOYEE_PUBLIC_KEY";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                    var pPubKey = new OracleParameter("p_pub_key", OracleDbType.Clob, ParameterDirection.Output);
                    cmd.Parameters.Add(pPubKey);

                    cmd.ExecuteNonQuery();

                    if (pPubKey.Value == DBNull.Value || pPubKey.Value == null)
                        return NotFound(new { message = $"Public key của Employee ID {empId} không tồn tại" });

                    // đọc CLOB
                    // Lấy public key từ CLOB
                    if (pPubKey.Value == DBNull.Value || pPubKey.Value == null)
                        return NotFound(new { message = $"Public key của Employee ID {empId} không tồn tại" });

                    var clobPubKey = (Oracle.ManagedDataAccess.Types.OracleClob)pPubKey.Value;
                    publicKey = clobPubKey.Value;  // Lấy toàn bộ text

                }

                // --- 3. GET_STOCKIN_SIGNATURE ---
                string signature;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_STOCKIN_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                    var pSig = new OracleParameter("p_signature", OracleDbType.Clob, ParameterDirection.Output);
                    cmd.Parameters.Add(pSig);

                    cmd.ExecuteNonQuery();

                    if (pSig.Value == DBNull.Value || pSig.Value == null)
                        return NotFound(new { message = $"Signature của StockIn ID {stockinId} không tồn tại" });

                    // Lấy signature từ CLOB
                    if (pSig.Value == DBNull.Value || pSig.Value == null)
                        return NotFound(new { message = $"Signature của StockIn ID {stockinId} không tồn tại" });

                    var clobSig = (Oracle.ManagedDataAccess.Types.OracleClob)pSig.Value;
                    signature = clobSig.Value;  // Lấy toàn bộ text

                }

                // --- 4. VERIFY_STOCKIN_SIGNATURE ---
                int isValid;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.VERIFY_STOCKIN_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                    cmd.Parameters.Add("p_public_key", OracleDbType.Varchar2, ParameterDirection.Input).Value = publicKey;
                    cmd.Parameters.Add("p_signature", OracleDbType.Clob, ParameterDirection.Input).Value = signature;

                    var pIsValid = new OracleParameter("p_is_valid", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pIsValid);

                    cmd.ExecuteNonQuery();
                    isValid = ((Oracle.ManagedDataAccess.Types.OracleDecimal)pIsValid.Value).ToInt32();
                }

                return Ok(new
                {
                    StockInId = stockinId,
                    IsValid = isValid == 1
                });
            }
            catch (OracleException ex)
            {
                return StatusCode(500, new { Message = "Oracle Error", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "General Error", Error = ex.Message });
            }
        }
    }


    // DTOs
    public class ImportItemDto
    {
        public string PartName { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public long Price { get; set; } 
    }

    public class ImportStockDto
    {
        public int StockInId { get; set; }
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public DateTime InDate { get; set; }
        public string PrivateKey { get; set; }
        public List<ImportItemDto> Items { get; set; }
    }
}
