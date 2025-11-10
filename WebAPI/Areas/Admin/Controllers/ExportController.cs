using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WebAPI.Helpers;

using WebAPI.Services;
using WebAPI.Models.Security;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly InvoicePdfService _invoicePdfService;

        public ExportController(OracleConnectionManager connManager, JwtHelper jwtHelper ,  OracleSessionHelper oracleSessionHelper, InvoicePdfService invoicePdfService)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
            _invoicePdfService = invoicePdfService;
        }

        // GET: api/admin/export/getallexport
        [HttpGet("getallexport")]
        [Authorize]
        public IActionResult GetAllExports()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_EXPORTS";
                cmd.CommandType = CommandType.StoredProcedure;

                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        StockOutId = reader["STOCKOUT_ID"],
                        EmpUsername = reader["EmpUsername"],
                        OutDate = reader["OUT_DATE"],
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

        [HttpGet("invoice/{stockoutId}")]
        [Authorize]
        public IActionResult GetExportInvoicePdf(int stockoutId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                // 1) Load export header + items
                var items = new List<ExportItemDto>();
                string? empUsername = null;
                DateTime? inDate = null;
                string note = null;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EXPORT_BY_ID";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                    var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                    cmd.Parameters.Add(outputCursor);

                    using var reader = cmd.ExecuteReader();
                    if (!reader.HasRows)
                        return NotFound(new { message = $"StockOut ID {stockoutId} not found" });

                    while (reader.Read())
                    {
                        if (empUsername == null)
                        {
                            empUsername = Convert.ToString(reader["EmpUsername"]);
                            inDate = Convert.ToDateTime(reader["OutDate"]);
                            note = reader["Note"]?.ToString();
                        }

                        items.Add(new ExportItemDto
                        {
                            PartName = reader["PartName"]?.ToString(),
                            Manufacturer = reader["Manufacturer"]?.ToString(),
                            Serial = reader["Serial"]?.ToString(),
                            Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                        });
                    }
                }

                var dto = new ExportStockDto
                {
                    StockOutId = stockoutId,
                    EmpUsername = empUsername ?? string.Empty,
                    OutDate = inDate ?? DateTime.MinValue,
                    Note = note,
                    Items = items
                };

                // 2) Load persisted signature
                string signature;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_STOCKOUT_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                    var pSig = new OracleParameter("p_signature", OracleDbType.Clob, ParameterDirection.Output);
                    cmd.Parameters.Add(pSig);
                    cmd.ExecuteNonQuery();

                    if (pSig.Value == DBNull.Value || pSig.Value == null)
                        signature = string.Empty;
                    else
                        signature = ((Oracle.ManagedDataAccess.Types.OracleClob)pSig.Value).Value;
                }

                // 3) Generate PDF without verify URL
                var pdfBytes = _invoicePdfService.GenerateExportInvoicePdf(dto, signature ?? string.Empty, null, null);
                return File(pdfBytes, "application/pdf", $"ExportInvoice_{stockoutId}.pdf");
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


        [HttpGet("details/{stockoutId}")]
        [Authorize]
        public IActionResult GetExportDetails(int stockoutId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_EXPORT_BY_ID";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var items = new List<ExportItemDto>();
                string? empUsername = null;
                DateTime? inDate = null;
                string note = null;

                using var reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                    return NotFound(new { message = $"StockOut ID {stockoutId} not found" });

                while (reader.Read())
                {
                    // Thông tin chung STOCK_IN (lấy 1 lần)
                    if (empUsername == null)
                    {
                        empUsername = Convert.ToString(reader["EmpUsername"]);
                        inDate = Convert.ToDateTime(reader["OutDate"]);
                        note = reader["Note"]?.ToString();
                    }

                    // Chi tiết từng item
                    items.Add(new ExportItemDto
                    {
                        PartName = reader["PartName"]?.ToString(),
                        Manufacturer = reader["Manufacturer"]?.ToString(),
                        Serial = reader["Serial"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                    });
                }

                var result = new ExportStockDto
                {
                    StockOutId = stockoutId,
                    EmpUsername = empUsername ?? "",
                    OutDate = inDate ?? DateTime.MinValue,
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




        [HttpPost("create")]
        [Authorize]
        public IActionResult CreateExportFromOrder([FromBody] CreateExportFromOrderDto request)
        {
            if (request.OrderId <= 0)
                return BadRequest("Missing OrderId");
            if (string.IsNullOrEmpty(request.PrivateKey))
                return BadRequest("Missing private key");

            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            using var transaction = conn.BeginTransaction();
            try
            {
                // First create stockout transaction with temporary empty signature
                string signatureBase64 = "TEST";

                // Normalize private key: strip PEM headers/footers and whitespace
                string NormalizePrivateKey(string key)
                {
                    if (string.IsNullOrWhiteSpace(key)) return key;
                    var k = key.Trim();
                    // Best-effort decode if URL-encoded
                    try { k = Uri.UnescapeDataString(k); } catch { }
                    // Remove PEM headers/footers if present
                    k = Regex.Replace(k, "-+BEGIN[^-]+-+", "", RegexOptions.IgnoreCase);
                    k = Regex.Replace(k, "-+END[^-]+-+", "", RegexOptions.IgnoreCase);
                    // Remove all whitespace (spaces, tabs, newlines)
                    k = Regex.Replace(k, "\\s+", "");
                    // Strip any characters not valid in base64 (including BOM/zero-width)
                    k = Regex.Replace(k, "[^A-Za-z0-9+/=]", "");
                    return k;
                }
                var normalizedPrivateKey = NormalizePrivateKey(request.PrivateKey);
                var stockOutIdParam = new OracleParameter("p_stockout_id", OracleDbType.Int32, ParameterDirection.Output);
                int empId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EMPLOYEE_ID_BY_USERNAME";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = request.EmpUsername;
                    var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pEmpId);

                    cmd.ExecuteNonQuery();
                    empId = Convert.ToInt32(pEmpId.Value.ToString());
                }
                // Giả định bạn có một procedure làm tất cả các bước
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "APP.CREATE_STOCKOUT_TRANSACTION"; // Procedure này cần được tạo trong DB
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = request.OrderId;
                    cmd.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                    string noteValue = "Xuất kho tự động cho Order ID " + request.OrderId.ToString();
                    cmd.Parameters.Add("p_note", OracleDbType.Varchar2, ParameterDirection.Input).Value = noteValue ?? "";
                    cmd.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = signatureBase64;
                    
                    cmd.Parameters.Add(stockOutIdParam);
                    cmd.ExecuteNonQuery();
                }

                var stockOutId = Convert.ToInt32(stockOutIdParam.Value.ToString());

                // Generate signature based on created stockout and its items
                string generatedSignature;
                using (var cmdSign = conn.CreateCommand())
                {
                    cmdSign.CommandText = "APP.SIGN_STOCKOUT";
                    cmdSign.CommandType = CommandType.StoredProcedure;
                    cmdSign.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                    cmdSign.Parameters.Add("p_private_key", OracleDbType.Varchar2, ParameterDirection.Input).Value = normalizedPrivateKey;
                    var outSignature = new OracleParameter("p_signature", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                    cmdSign.Parameters.Add(outSignature);
                    cmdSign.ExecuteNonQuery();
                    generatedSignature = outSignature.Value?.ToString() ?? "";
                }

                // Update stockout signature
                using (var cmdUpd = conn.CreateCommand())
                {
                    cmdUpd.CommandText = "APP.UPDATE_STOCKOUT_SIGNATURE";
                    cmdUpd.CommandType = CommandType.StoredProcedure;
                    cmdUpd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                    cmdUpd.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = generatedSignature ?? "";
                    cmdUpd.ExecuteNonQuery();
                }

                transaction.Commit();
                return Ok(new { Message = "Export created from order successfully.", StockOutId = stockOutId });
            }
            catch (OracleException ex)
            {
                transaction.Rollback();
                return StatusCode(500, new { Message = "Oracle Error - Transaction rolled back", ErrorCode = ex.Number, Error = ex.Message });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return StatusCode(500, new { Message = "General Error - Transaction rolled back", Error = ex.Message });
            }
        }

        [HttpPost("create-secure")]
        [Authorize]
        public IActionResult CreateExportFromOrderSecure([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(new { Message = "Invalid encrypted payload" });

            try
            {
                byte[] keyBlock = rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));
                byte[] aesKey = new byte[32];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(keyBlock, 0, aesKey, 0, 32);
                Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

                byte[] cipherBytes = Convert.FromBase64String(payload.CipherDataBase64);
                using (Aes aes = Aes.Create())
                {
                    ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        string plaintext = reader.ReadToEnd();
                        var dto = System.Text.Json.JsonSerializer.Deserialize<CreateExportFromOrderDto>(plaintext);
                        if (dto == null) return BadRequest(new { Message = "Cannot parse payload" });
                        return CreateExportFromOrder(dto);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Decrypt failed", Error = ex.Message });
            }
        }
        [HttpGet("verifysign/{stockoutId}")]
        [Authorize]
        public IActionResult VerifyStockInSignature(int stockoutId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {

                // --- 1. GET_EMP_ID_FROM_STOCKIN ---
                int empId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_EMP_ID_FROM_STOCKOUT";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                    var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pEmpId);

                    cmd.ExecuteNonQuery();

                    if (pEmpId.Value == DBNull.Value || pEmpId.Value == null)
                        return NotFound(new { message = $"StockOut ID {stockoutId} không tồn tại" });

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
                    cmd.CommandText = "APP.GET_STOCKOUT_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                    var pSig = new OracleParameter("p_signature", OracleDbType.Clob, ParameterDirection.Output);
                    cmd.Parameters.Add(pSig);

                    cmd.ExecuteNonQuery();

                    if (pSig.Value == DBNull.Value || pSig.Value == null)
                        return NotFound(new { message = $"Signature của StockOut ID {stockoutId} không tồn tại" });

                    // Lấy signature từ CLOB
                    if (pSig.Value == DBNull.Value || pSig.Value == null)
                        return NotFound(new { message = $"Signature của StockOut ID {stockoutId} không tồn tại" });

                    var clobSig = (Oracle.ManagedDataAccess.Types.OracleClob)pSig.Value;
                    signature = clobSig.Value;  // Lấy toàn bộ text

                }

                // --- 4. VERIFY_STOCKIN_SIGNATURE ---
                int isValid;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.VERIFY_STOCKOUT_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                    cmd.Parameters.Add("p_public_key", OracleDbType.Varchar2, ParameterDirection.Input).Value = publicKey;
                    cmd.Parameters.Add("p_signature", OracleDbType.Clob, ParameterDirection.Input).Value = signature;

                    var pIsValid = new OracleParameter("p_is_valid", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pIsValid);

                    cmd.ExecuteNonQuery();
                    isValid = ((Oracle.ManagedDataAccess.Types.OracleDecimal)pIsValid.Value).ToInt32();
                }

                return Ok(new
                {
                    StockInId = stockoutId,
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
    public class ExportItemDto
    {
        public string PartName { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public long Price { get; set; } 
    }

    public class ExportStockDto
    {
        public int StockOutId { get; set; }
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public DateTime OutDate { get; set; }
        public string PrivateKey { get; set; }
        public List<ExportItemDto> Items { get; set; }
    }
    public class CreateExportFromOrderDto
    {
        public string EmpUsername { get; set;}
        public int OrderId { get; set; }
        public string PrivateKey { get; set; }
    }
}
