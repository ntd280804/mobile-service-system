using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WebAPI.Helpers;

using WebAPI.Services;
using WebAPI.Models.Security;
using WebAPI.Models;

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
        public IActionResult GetSignedExportInvoicePdf(int stockoutId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                byte[] pdfBytes = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_STOCKOUT_PDF";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockoutId;
                    var pPdf = new OracleParameter("p_pdf", OracleDbType.Blob, ParameterDirection.Output);
                    cmd.Parameters.Add(pPdf);

                    cmd.ExecuteNonQuery();

                    if (pPdf.Value == null || pPdf.Value == DBNull.Value)
                        return NotFound(new { message = $"Signed PDF for StockOut ID {stockoutId} not found" });

                    using (var blob = (Oracle.ManagedDataAccess.Types.OracleBlob)pPdf.Value)
                    {
                        pdfBytes = blob?.Value;
                    }
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                    return NotFound(new { message = $"Signed PDF for StockOut ID {stockoutId} is empty" });

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
        public IActionResult CreateExportFromOrder([FromBody] CreateExportFromOrderDto dto)
        {
            if (dto.OrderId <= 0)
                return BadRequest("Missing OrderId");
            bool hasProvidedPfx = !string.IsNullOrWhiteSpace(dto.CertificatePfxBase64) && !string.IsNullOrWhiteSpace(dto.CertificatePassword);
            if (!hasProvidedPfx)
            {
                throw new InvalidOperationException(
                    "Không thể tạo PFX hợp lệ khi chỉ có private key. Vui lòng cung cấp PFX do CA cấp.");
            }
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            using var transaction = conn.BeginTransaction();
            try
            {
                // First create stockout transaction with temporary empty signature
                string signatureBase64 = "TEST";

                var stockOutIdParam = new OracleParameter("p_stockout_id", OracleDbType.Int32, ParameterDirection.Output);
                int empId;
                using (var cmdEmp = conn.CreateCommand())
                {
                    cmdEmp.CommandText = "APP.GET_EMPLOYEE_ID_BY_USERNAME";
                    cmdEmp.CommandType = CommandType.StoredProcedure;

                    cmdEmp.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.EmpUsername;
                    var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmdEmp.Parameters.Add(pEmpId);

                    cmdEmp.ExecuteNonQuery();
                    empId = Convert.ToInt32(pEmpId.Value.ToString());
                }
                // Giả định bạn có một procedure làm tất cả các bước
                using (var cmdStockOut = conn.CreateCommand())
                {
                    cmdStockOut.Transaction = transaction;
                    cmdStockOut.CommandText = "APP.CREATE_STOCKOUT_TRANSACTION"; // Procedure này cần được tạo trong DB
                    cmdStockOut.CommandType = CommandType.StoredProcedure;
                    cmdStockOut.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = dto.OrderId;
                    cmdStockOut.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                    string noteValue = "Xuất kho tự động cho Order ID " + dto.OrderId.ToString();
                    cmdStockOut.Parameters.Add("p_note", OracleDbType.Varchar2, ParameterDirection.Input).Value = noteValue ?? "";
                    cmdStockOut.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = signatureBase64;
                    
                    cmdStockOut.Parameters.Add(stockOutIdParam);
                    cmdStockOut.ExecuteNonQuery();
                }

                var stockOutId = Convert.ToInt32(stockOutIdParam.Value.ToString());

                // Ký số nghiệp vụ không cần thiết khi dùng PFX certificate để ký PDF

                // Create invoice from stockout
                int invoiceId;
                using (var cmdInvoice = conn.CreateCommand())
                {
                    cmdInvoice.CommandText = "APP.CREATE_INVOICE";
                    cmdInvoice.CommandType = CommandType.StoredProcedure;
                    cmdInvoice.Transaction = transaction;
                    cmdInvoice.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                    var pInvoiceId = new OracleParameter("p_invoice_id", OracleDbType.Int32, ParameterDirection.Output);
                    cmdInvoice.Parameters.Add(pInvoiceId);
                    cmdInvoice.ExecuteNonQuery();
                    invoiceId = Convert.ToInt32(pInvoiceId.Value.ToString());
                }

                transaction.Commit();
                
                // Reload data to render PDF
                using (var cmdGetExport = conn.CreateCommand())
                {
                    cmdGetExport.CommandText = "APP.GET_EXPORT_BY_ID";
                    cmdGetExport.CommandType = CommandType.StoredProcedure;

                    cmdGetExport.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                    var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                    cmdGetExport.Parameters.Add(outputCursor);

                    var items = new List<ExportItemDto>();
                    string? empUsername = null;
                    DateTime? inDate = null;
                    string note = null;

                    using var reader = cmdGetExport.ExecuteReader();

                    if (!reader.HasRows)
                        return NotFound(new { message = $"StockOut ID {stockOutId} not found" });

                    while (reader.Read())
                    {
                        // Thông tin chung STOCK_OUT (lấy 1 lần)
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

                    var pdfDto = new ExportStockDto
                    {
                        StockOutId = stockOutId,
                        EmpUsername = empUsername ?? string.Empty,
                        OutDate = inDate ?? DateTime.MinValue,
                        Note = note,
                        Items = items
                    };
                    
                    // Sử dụng PFX certificate từ CA
                    byte[] pfxBytes;
                    string pfxPassword = dto.CertificatePassword;
                    try
                    {
                        pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
                    }
                    catch
                    {
                        return BadRequest("Invalid certificate PFX base64.");
                    }

                    var signedExportPdf = _invoicePdfService.GenerateExportInvoicePdfAndSignWithCertificate(
                        pdfDto,
                        pfxBytes,
                        pfxPassword,
                        cmd =>
                        {
                            cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                        },
                        null,
                        null
                    );

                    // Load invoice data and generate invoice PDF
                    var invoiceDto = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                    if (invoiceDto != null)
                    {
                        var signedInvoicePdf = _invoicePdfService.GenerateInvoicePdfAndSignWithCertificate(
                            invoiceDto,
                            pfxBytes,
                            pfxPassword,
                            cmd =>
                            {
                                cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId;
                            },
                            null,
                            null
                        );
                        var fileNameInvoice = $"Invoice_{invoiceId}.pdf";
                        return File(signedInvoicePdf, "application/pdf", fileNameInvoice);
                    }

                    var fileNameOut = $"ExportInvoice_{stockOutId}.pdf";
                    return File(signedExportPdf, "application/pdf", fileNameOut);
                }
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

        [HttpPost("create-secure-encrypted")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> CreateExportFromOrderSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                // Giải mã request từ client
                byte[] keyBlock = rsaKeyService.DecryptKeyBlock(Convert.FromBase64String(payload.EncryptedKeyBlockBase64));
                byte[] aesKey = new byte[32];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(keyBlock, 0, aesKey, 0, 32);
                Buffer.BlockCopy(keyBlock, 32, iv, 0, 16);

                byte[] cipherBytes = Convert.FromBase64String(payload.CipherDataBase64);
                string plaintext;
                using (Aes aes = Aes.Create())
                {
                    ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        plaintext = reader.ReadToEnd();
                    }
                }

                var dto = System.Text.Json.JsonSerializer.Deserialize<CreateExportFromOrderDto>(plaintext);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                // Lấy clientId từ session headers
                var username = HttpContext.Request.Headers["X-Oracle-Username"].ToString();
                var platform = HttpContext.Request.Headers["X-Oracle-Platform"].ToString();
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(platform))
                    return StatusCode(500, ApiResponse<EncryptedPayload>.Fail("Cannot determine clientId from session headers"));
                
                // Sử dụng clientId với prefix "admin-" để lấy public key từ DB
                string clientId = "admin-" + username;
                
                // Đảm bảo public key đã được load từ DB vào RsaKeyService
                if (!rsaKeyService.TryGetClientPublicKey(clientId, out _))
                {
                    // Lấy public key từ DB và lưu vào RsaKeyService
                    var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                    if (conn != null)
                    {
                        try
                        {
                            using var cmd = new OracleCommand("APP.GET_PUBLICKEY_BY_USERNAME", conn);
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                            var pPubKey = new OracleParameter("p_public_key", OracleDbType.Clob, ParameterDirection.Output);
                            cmd.Parameters.Add(pPubKey);
                            cmd.ExecuteNonQuery();

                            if (pPubKey.Value != DBNull.Value && pPubKey.Value != null)
                            {
                                var clobPubKey = (Oracle.ManagedDataAccess.Types.OracleClob)pPubKey.Value;
                                string publicKeyFromDb = clobPubKey.Value?.Trim() ?? "";
                                if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                                {
                                    // Normalize public key: loại bỏ whitespace và kiểm tra format
                                    string normalizedPublicKey = publicKeyFromDb
                                        .Replace("\r", "")
                                        .Replace("\n", "")
                                        .Replace(" ", "")
                                        .Replace("\t", "");
                                    
                                    // Nếu có PEM headers, extract Base64
                                    if (normalizedPublicKey.Contains("BEGIN") || normalizedPublicKey.Contains("END"))
                                    {
                                        // Extract Base64 từ PEM format
                                        int startIdx = normalizedPublicKey.IndexOf("-----BEGIN");
                                        int endIdx = normalizedPublicKey.IndexOf("-----END");
                                        if (startIdx >= 0 && endIdx > startIdx)
                                        {
                                            int beginEnd = normalizedPublicKey.IndexOf("-----", startIdx + 10);
                                            if (beginEnd > startIdx)
                                            {
                                                normalizedPublicKey = normalizedPublicKey.Substring(beginEnd + 5, endIdx - beginEnd - 5)
                                                    .Replace("\r", "")
                                                    .Replace("\n", "")
                                                    .Replace(" ", "");
                                            }
                                        }
                                    }
                                    
                                    rsaKeyService.SaveClientPublicKey(clientId, normalizedPublicKey);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error nhưng tiếp tục, sẽ throw error sau nếu không có public key
                        }
                    }
                }

                // Xử lý export - cần generate PDF trực tiếp để mã hóa response
                try
                {
                    var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                    if (conn == null)
                    {
                        // Mã hóa error message
                        var errorObj = new { Success = false, Message = "Unauthorized" };
                        string errorJson = System.Text.Json.JsonSerializer.Serialize(errorObj);
                        var encryptedError = rsaKeyService.EncryptForClient(clientId, errorJson);
                        return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                        {
                            EncryptedKeyBlockBase64 = encryptedError.EncryptedKeyBlockBase64,
                            CipherDataBase64 = encryptedError.CipherDataBase64
                        }));
                    }

                    // Execute export và get stockOutId, invoiceId
                    int stockOutId = 0;
                    int invoiceId = 0;
                    using var transaction = conn.BeginTransaction();
                    try
                    {
                        int empId;
                        using (var cmdEmp = conn.CreateCommand())
                        {
                            cmdEmp.Transaction = transaction;
                            cmdEmp.CommandText = "APP.GET_EMPLOYEE_ID_BY_USERNAME";
                            cmdEmp.CommandType = CommandType.StoredProcedure;
                            cmdEmp.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.EmpUsername;
                            var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                            cmdEmp.Parameters.Add(pEmpId);
                            cmdEmp.ExecuteNonQuery();
                            empId = Convert.ToInt32(pEmpId.Value.ToString());
                        }

                        string signatureBase64 = "TEST";
                        var stockOutIdParam = new OracleParameter("p_stockout_id", OracleDbType.Int32, ParameterDirection.Output);
                        using (var cmdStockOut = conn.CreateCommand())
                        {
                            cmdStockOut.Transaction = transaction;
                            cmdStockOut.CommandText = "APP.CREATE_STOCKOUT_TRANSACTION";
                            cmdStockOut.CommandType = CommandType.StoredProcedure;
                            cmdStockOut.Parameters.Add("p_order_id", OracleDbType.Int32, ParameterDirection.Input).Value = dto.OrderId;
                            cmdStockOut.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                            string noteValue = "Xuất kho tự động cho Order ID " + dto.OrderId.ToString();
                            cmdStockOut.Parameters.Add("p_note", OracleDbType.Varchar2, ParameterDirection.Input).Value = noteValue ?? "";
                            cmdStockOut.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = signatureBase64;
                            cmdStockOut.Parameters.Add(stockOutIdParam);
                            cmdStockOut.ExecuteNonQuery();
                        }

                        stockOutId = Convert.ToInt32(stockOutIdParam.Value.ToString());

                        // Create invoice from stockout
                        using (var cmdInvoice = conn.CreateCommand())
                        {
                            cmdInvoice.CommandText = "APP.CREATE_INVOICE";
                            cmdInvoice.CommandType = CommandType.StoredProcedure;
                            cmdInvoice.Transaction = transaction;
                            cmdInvoice.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                            var pInvoiceId = new OracleParameter("p_invoice_id", OracleDbType.Int32, ParameterDirection.Output);
                            cmdInvoice.Parameters.Add(pInvoiceId);
                            cmdInvoice.ExecuteNonQuery();
                            invoiceId = Convert.ToInt32(pInvoiceId.Value.ToString());
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }

                    // Generate Export PDF
                    var items = new List<ExportItemDto>();
                    string? empUsername = null;
                    DateTime? outDate = null;
                    string note = null;
                    using (var cmdGetExport = conn.CreateCommand())
                    {
                        cmdGetExport.CommandText = "APP.GET_EXPORT_BY_ID";
                        cmdGetExport.CommandType = CommandType.StoredProcedure;
                        cmdGetExport.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId;
                        var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                        cmdGetExport.Parameters.Add(outputCursor);
                        using var reader = cmdGetExport.ExecuteReader();
                        while (reader.Read())
                        {
                            if (empUsername == null)
                            {
                                empUsername = Convert.ToString(reader["EmpUsername"]);
                                outDate = Convert.ToDateTime(reader["OutDate"]);
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

                    var pdfDto = new ExportStockDto
                    {
                        StockOutId = stockOutId,
                        EmpUsername = empUsername ?? string.Empty,
                        OutDate = outDate ?? DateTime.MinValue,
                        Note = note,
                        Items = items
                    };

                    byte[] pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
                    string pfxPassword = dto.CertificatePassword;
                    var signedExportPdf = _invoicePdfService.GenerateExportInvoicePdfAndSignWithCertificate(
                        pdfDto, pfxBytes, pfxPassword,
                        cmd => cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId,
                        null, null);

                    // Load invoice data and generate invoice PDF
                    var invoiceDto = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                    byte[] signedInvoicePdf = null;
                    if (invoiceDto != null)
                    {
                        signedInvoicePdf = _invoicePdfService.GenerateInvoicePdfAndSignWithCertificate(
                            invoiceDto, pfxBytes, pfxPassword,
                            cmd => cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId,
                            null, null);
                    }

                    // Tạo response object chứa PDF base64 và filename
                    var responseObj = new
                    {
                        Success = true,
                        Type = "pdf",
                        ExportPdfBase64 = Convert.ToBase64String(signedExportPdf),
                        ExportFileName = $"ExportInvoice_{stockOutId}.pdf",
                        InvoicePdfBase64 = signedInvoicePdf != null ? Convert.ToBase64String(signedInvoicePdf) : null,
                        InvoiceFileName = signedInvoicePdf != null ? $"Invoice_{invoiceId}.pdf" : null,
                        InvoiceId = invoiceId,
                        StockOutId = stockOutId
                    };

                    string responseJson = System.Text.Json.JsonSerializer.Serialize(responseObj);
                    var encryptedResponse = rsaKeyService.EncryptForClient(clientId, responseJson);
                    return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                    {
                        EncryptedKeyBlockBase64 = encryptedResponse.EncryptedKeyBlockBase64,
                        CipherDataBase64 = encryptedResponse.CipherDataBase64
                    }));
                }
                catch (Exception ex)
                {
                    // Nếu có lỗi, mã hóa error message
                    var errorObj = new { Success = false, Message = ex.Message };
                    string errorJson = System.Text.Json.JsonSerializer.Serialize(errorObj);
                    var encryptedError = rsaKeyService.EncryptForClient(clientId, errorJson);
                    return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                    {
                        EncryptedKeyBlockBase64 = encryptedError.EncryptedKeyBlockBase64,
                        CipherDataBase64 = encryptedError.CipherDataBase64
                    }));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
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
        public List<ExportItemDto> Items { get; set; }
    }
    public class CreateExportFromOrderDto
    {
        public string EmpUsername { get; set;}
        public int OrderId { get; set; }
        // Required: PFX certificate từ CA để ký PDF
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
    }
    
}
