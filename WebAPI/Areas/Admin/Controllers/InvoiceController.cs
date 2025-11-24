using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WebAPI.Helpers;
using WebAPI.Services;
using WebAPI.Models.Security;
using WebAPI.Models;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly InvoicePdfService _invoicePdfService;

        public InvoiceController(
            OracleConnectionManager connManager,
            OracleSessionHelper oracleSessionHelper,
            InvoicePdfService invoicePdfService)
        {
            _connManager = connManager;
            _oracleSessionHelper = oracleSessionHelper;
            _invoicePdfService = invoicePdfService;
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAllInvoices()
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_ALL_INVOICES";
                cmd.CommandType = CommandType.StoredProcedure;

                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var result = new List<object>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        InvoiceId = reader["INVOICE_ID"] != DBNull.Value ? Convert.ToInt32(reader["INVOICE_ID"]) : 0,
                        StockOutId = reader["STOCKOUT_ID"] != DBNull.Value ? Convert.ToInt32(reader["STOCKOUT_ID"]) : 0,
                        CustomerPhone = reader["CUSTOMER_PHONE"]?.ToString(),
                        EmpUsername = reader["EmpUsername"]?.ToString(),
                        InvoiceDate = reader["INVOICE_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["INVOICE_DATE"]) : DateTime.MinValue,
                        TotalAmount = reader["TOTAL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["TOTAL_AMOUNT"]) : 0,
                        Status = reader["STATUS"]?.ToString()
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpGet("{invoiceId}")]
        [Authorize]
        public IActionResult GetInvoiceDetails(int invoiceId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                var invoice = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                if (invoice == null)
                    return NotFound(new { message = $"Invoice {invoiceId} not found" });

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpGet("{invoiceId}/pdf")]
        [Authorize]
        public IActionResult GetInvoicePdf(int invoiceId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                byte[] pdfBytes = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_INVOICE_PDF";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId;
                    var pPdf = new OracleParameter("p_pdf", OracleDbType.Blob, ParameterDirection.Output);
                    cmd.Parameters.Add(pPdf);

                    cmd.ExecuteNonQuery();

                    if (pPdf.Value == null || pPdf.Value == DBNull.Value)
                        return NotFound(new { message = $"Signed PDF for Invoice ID {invoiceId} not found" });

                    using var blob = (OracleBlob)pPdf.Value;
                    pdfBytes = blob?.Value;
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                    return NotFound(new { message = $"Signed PDF for Invoice ID {invoiceId} is empty" });

                return File(pdfBytes, "application/pdf", $"Invoice_{invoiceId}.pdf");
            }
            catch (OracleException ex) when (ex.Number == 28)
            {
                _oracleSessionHelper.TryGetSession(HttpContext, out var username, out var platform, out var sessionId);
                _oracleSessionHelper.HandleSessionKilled(HttpContext, _connManager, username, platform, sessionId);
                return Unauthorized(new { message = "Phiên Oracle đã bị kill. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy PDF hóa đơn", detail = ex.Message });
            }
        }
        [HttpPost("{invoiceId}/generate-pdf-secure-encrypted")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> GenerateInvoicePdfSecureEncrypted(
            [FromServices] RsaKeyService rsaKeyService,
            int invoiceId,
            [FromBody] EncryptedPayload payload)
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

                var dto = System.Text.Json.JsonSerializer.Deserialize<GenerateInvoicePdfDto>(plaintext);
                if (dto == null)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Cannot parse payload"));

                if (dto.InvoiceId != invoiceId)
                    return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invoice ID mismatch"));

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

                // Generate PDF invoice
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

                    // Load invoice data
                    var invoiceDto = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                    if (invoiceDto == null)
                    {
                        var errorObj = new { Success = false, Message = $"Invoice {invoiceId} not found" };
                        string errorJson = System.Text.Json.JsonSerializer.Serialize(errorObj);
                        var encryptedError = rsaKeyService.EncryptForClient(clientId, errorJson);
                        return Ok(ApiResponse<EncryptedPayload>.Ok(new EncryptedPayload
                        {
                            EncryptedKeyBlockBase64 = encryptedError.EncryptedKeyBlockBase64,
                            CipherDataBase64 = encryptedError.CipherDataBase64
                        }));
                    }

                    byte[] pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
                    string pfxPassword = dto.CertificatePassword;
                    var signedInvoicePdf = _invoicePdfService.GenerateInvoicePdfAndSignWithCertificate(
                        invoiceDto, pfxBytes, pfxPassword,
                        cmd => cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId,
                        null, null);

                    // Tạo response object chứa PDF base64 và filename
                    var responseObj = new
                    {
                        Success = true,
                        Type = "pdf",
                        PdfBase64 = Convert.ToBase64String(signedInvoicePdf),
                        FileName = $"Invoice_{invoiceId}.pdf"
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
    public class GenerateInvoicePdfDto
    {
        public int InvoiceId { get; set; }
        // Required: PFX certificate từ CA để ký PDF
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
    }
}

