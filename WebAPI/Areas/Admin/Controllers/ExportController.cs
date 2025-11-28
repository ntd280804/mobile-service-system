using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Models.Export;
using WebAPI.Models.Security;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly ControllerHelper _helper;
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly PdfService _invoicePdfService;

        public ExportController(
            ControllerHelper helper,
            OracleConnectionManager connManager,
            JwtHelper jwtHelper,
            OracleSessionHelper oracleSessionHelper,
            PdfService invoicePdfService)
        {
            _helper = helper;
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _oracleSessionHelper = oracleSessionHelper;
            _invoicePdfService = invoicePdfService;
        }

        // GET: api/admin/export/getallexport
        [HttpGet]
        [Authorize]
        public IActionResult GetAllExports()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var result = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_ALL_EXPORTS",
                    "cur_out",
                    reader => new
                    {
                        StockOutId = reader["STOCKOUT_ID"],
                        EmpUsername = reader["EmpUsername"],
                        OutDate = reader["OUT_DATE"],
                        Note = reader["NOTE"]
                    });

                return Ok(result);
            }, "Lỗi khi lấy danh sách phiếu xuất");
        }


        [HttpGet("{stockoutId}/invoice")]
        [Authorize]
        public IActionResult GetSignedExportInvoicePdf(int stockoutId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                byte[]? pdfBytes = OracleHelper.ExecuteBlobOutput(
                    conn,
                    "APP.GET_STOCKOUT_PDF",
                    "p_pdf",
                    ("p_stockout_id", OracleDbType.Int32, stockoutId));

                if (pdfBytes == null || pdfBytes.Length == 0)
                    return NotFound(new { message = $"Signed PDF for StockOut ID {stockoutId} not found or empty" });

                return File(pdfBytes, "application/pdf", $"ExportInvoice_{stockoutId}.pdf");
            }, "Lỗi lấy file PDF đã ký phiếu xuất");
        }


        [HttpGet("{stockoutId}/details")]
        [Authorize]
        public IActionResult GetExportDetails(int stockoutId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var rows = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_EXPORT_BY_ID",
                    "cur_out",
                    reader => new
                    {
                        EmpUsername = reader["EmpUsername"]?.ToString(),
                        OutDate = reader["OutDate"] != DBNull.Value ? Convert.ToDateTime(reader["OutDate"]) : (DateTime?)null,
                        Note = reader["Note"]?.ToString(),
                        PartName = reader["PartName"]?.ToString(),
                        Manufacturer = reader["Manufacturer"]?.ToString(),
                        Serial = reader["Serial"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                    },
                    ("p_stockout_id", OracleDbType.Int32, stockoutId));

                if (rows.Count == 0)
                    return NotFound(new { message = $"StockOut ID {stockoutId} not found" });

                var firstRow = rows[0];
                var items = rows.Select(r => new ExportItemDto
                {
                    PartName = r.PartName,
                    Manufacturer = r.Manufacturer,
                    Serial = r.Serial,
                    Price = r.Price
                }).ToList();

                var result = new ExportStockDto
                {
                    StockOutId = stockoutId,
                    EmpUsername = firstRow.EmpUsername ?? "",
                    OutDate = firstRow.OutDate ?? DateTime.MinValue,
                    Note = firstRow.Note,
                    Items = items
                };

                return Ok(result);
            }, "Lỗi chi tiết phiếu xuất");
        }


        [HttpGet("{stockoutId}/verify")]
        [Authorize]
        public IActionResult VerifyStockOutSignature(int stockoutId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                

                // 3. Get signature
                string? signature = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.GET_STOCKOUT_SIGNATURE",
                    "p_signature",
                    ("p_stockout_id", OracleDbType.Int32, stockoutId));

                if (string.IsNullOrEmpty(signature))
                    return NotFound(new { message = $"Signature của StockOut ID {stockoutId} không tồn tại" });

                string empIdStr = signature.Split('-')[0];  // Lấy phần trước dấu "-"
                int empId = int.Parse(empIdStr);            // Chuyển sang int
                signature = signature.Split('-')[1];        // Lấy phần sau dấu "-" (chữ ký)
                string? publicKey = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.GET_EMPLOYEE_PUBLIC_KEY",
                    "p_pub_key",
                    ("p_emp_id", OracleDbType.Int32, empId));

                if (string.IsNullOrEmpty(publicKey))
                    return NotFound(new { message = $"Public key của Employee ID {empId} không tồn tại" });
                // 4. Verify signature
                var verifyOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.VERIFY_STOCKOUT_SIGNATURE",
                    new[]
                    {
                        ("p_stockout_id", OracleDbType.Int32, (object)stockoutId),
                        ("p_public_key", OracleDbType.Varchar2, publicKey),
                        ("p_signature", OracleDbType.Clob, signature)
                    },
                    new[] { ("p_is_valid", OracleDbType.Int32) });

                int isValid = ((Oracle.ManagedDataAccess.Types.OracleDecimal)verifyOutput["p_is_valid"]).ToInt32();

                return Ok(new
                {
                    StockInId = stockoutId,
                    IsValid = isValid == 1
                });
            }, "Lỗi xác thực chữ ký phiếu xuất");
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
                return BadRequest("Không thể tạo PFX hợp lệ khi chỉ có private key. Vui lòng cung cấp PFX do CA cấp.");
            }

            if (!TryValidateCertificate(dto.CertificatePfxBase64, dto.CertificatePassword, out var certificateBytes, out var certificateError))
            {
                return BadRequest(certificateError);
            }

            var certificatePassword = dto.CertificatePassword ?? "";

            return _helper.ExecuteWithTransaction(HttpContext, (conn, transaction) =>
            {
                // 1. Get employee ID
                var empIdOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.GET_EMPLOYEE_ID_BY_USERNAME",
                    new[] { ("p_username", OracleDbType.Varchar2, (object)(dto.EmpUsername ?? "")) },
                    new[] { ("p_emp_id", OracleDbType.Int32) });
                
                int empId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)empIdOutput["p_emp_id"]).ToInt32();

                // 2. Create stockout transaction
                string signatureBase64 = "TEST";
                string noteValue = "Xuất kho tự động cho Order ID " + dto.OrderId.ToString();
                var stockOutOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.CREATE_STOCKOUT_TRANSACTION",
                    new[]
                    {
                        ("p_order_id", OracleDbType.Int32, (object)dto.OrderId),
                        ("p_emp_id", OracleDbType.Int32, (object)empId),
                        ("p_note", OracleDbType.Varchar2, noteValue),
                        ("p_signature", OracleDbType.Varchar2, signatureBase64)
                    },
                    new[] { ("p_stockout_id", OracleDbType.Int32) },
                    transaction);
                
                int stockOutId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)stockOutOutput["p_stockout_id"]).ToInt32();

                // 3. Create invoice
                var invoiceOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.CREATE_INVOICE",
                    new[] { ("p_stockout_id", OracleDbType.Int32, (object)stockOutId) },
                    new[] { ("p_invoice_id", OracleDbType.Int32) },
                    transaction);
                
                int invoiceId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)invoiceOutput["p_invoice_id"]).ToInt32();

                // Transaction will be committed by ExecuteWithTransaction
                // Now reload data for PDF generation (outside transaction)
                var rows = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_EXPORT_BY_ID",
                    "cur_out",
                    reader => new
                    {
                        EmpUsername = reader["EmpUsername"]?.ToString(),
                        OutDate = reader["OutDate"] != DBNull.Value ? Convert.ToDateTime(reader["OutDate"]) : (DateTime?)null,
                        Note = reader["Note"]?.ToString(),
                        PartName = reader["PartName"]?.ToString(),
                        Manufacturer = reader["Manufacturer"]?.ToString(),
                        Serial = reader["Serial"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                    },
                    ("p_stockout_id", OracleDbType.Int32, stockOutId));

                if (rows.Count == 0)
                    return NotFound(new { message = $"StockOut ID {stockOutId} not found" });

                var firstRow = rows.FirstOrDefault();
                var items = rows.Select(r => new ExportItemDto
                {
                    PartName = r.PartName,
                    Manufacturer = r.Manufacturer,
                    Serial = r.Serial,
                    Price = r.Price
                }).ToList();

                var pdfDto = new ExportStockDto
                {
                    StockOutId = stockOutId,
                    EmpUsername = firstRow?.EmpUsername ?? "",
                    OutDate = firstRow?.OutDate ?? DateTime.MinValue,
                    Note = firstRow?.Note,
                    Items = items
                };

                // Generate PDFs
                var signedExportPdf = _invoicePdfService.GenerateExportInvoicePdfAndSignWithCertificate(
                    pdfDto,
                    certificateBytes,
                    certificatePassword,
                    cmd => cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId,
                    null,
                    null);

                // Load invoice data and generate invoice PDF
                var invoiceDto = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                if (invoiceDto != null)
                {
                    var signedInvoicePdf = _invoicePdfService.GenerateInvoicePdfAndSignWithCertificate(
                        invoiceDto,
                            certificateBytes,
                            certificatePassword,
                        cmd => cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId,
                        null,
                        null);
                    return File(signedInvoicePdf, "application/pdf", $"Invoice_{invoiceId}.pdf");
                }

                return File(signedExportPdf, "application/pdf", $"ExportInvoice_{stockOutId}.pdf");
            }, "Lỗi tạo phiếu xuất");
        }

        [HttpPost("create/secure")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> CreateExportFromOrderSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                var dto = SecurePayloadHelper.DecryptPayload<CreateExportFromOrderDto>(rsaKeyService, payload);
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
                    var connPubKey = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorizedPubKey);
                    if (connPubKey != null)
                    {
                        try
                        {
                            string? publicKeyFromDb = OracleHelper.ExecuteClobOutput(
                                connPubKey,
                                "APP.GET_PUBLICKEY_BY_USERNAME",
                                "p_public_key",
                                ("p_username", OracleDbType.Varchar2, username));

                            if (!string.IsNullOrWhiteSpace(publicKeyFromDb))
                            {
                                // Normalize public key
                                string normalizedPublicKey = publicKeyFromDb
                                    .Replace("\r", "")
                                    .Replace("\n", "")
                                    .Replace(" ", "")
                                    .Replace("\t", "");

                                // Extract Base64 từ PEM format nếu có
                                if (normalizedPublicKey.Contains("BEGIN") || normalizedPublicKey.Contains("END"))
                                {
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
                        catch
                        {
                            // Log error nhưng tiếp tục
                        }
                    }
                }

                if (!TryValidateCertificate(dto.CertificatePfxBase64, dto.CertificatePassword, out var certificateBytes, out var certificateError))
                {
                    var encryptedError = SecurePayloadHelper.EncryptResponse(
                        rsaKeyService,
                        clientId,
                        new { Success = false, Message = certificateError });
                    return Ok(encryptedError);
                }

                var certificatePassword = dto.CertificatePassword ?? "";

                // Xử lý export - cần generate PDF trực tiếp để mã hóa response
                var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
                if (conn == null)
                {
                    var encryptedError = SecurePayloadHelper.EncryptResponse(
                        rsaKeyService,
                        clientId,
                        new { Success = false, Message = "Unauthorized" });
                    return Ok(encryptedError);
                }

                OracleTransaction? transaction = null;
                try
                {
                    transaction = conn.BeginTransaction();
                    int stockOutId = 0;
                    int invoiceId = 0;

                    // 1. Get employee ID
                    var empIdOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                        conn,
                        "APP.GET_EMPLOYEE_ID_BY_USERNAME",
                        new[] { ("p_username", OracleDbType.Varchar2, (object)(dto.EmpUsername ?? "")) },
                        new[] { ("p_emp_id", OracleDbType.Int32) },
                        transaction);
                    
                    int empId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)empIdOutput["p_emp_id"]).ToInt32();

                    // 2. Create stockout transaction
                    string signatureBase64 = "TEST";
                    string noteValue = "Xuất kho tự động cho Order ID " + dto.OrderId.ToString();
                    var stockOutOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                        conn,
                        "APP.CREATE_STOCKOUT_TRANSACTION",
                        new[]
                        {
                            ("p_order_id", OracleDbType.Int32, (object)dto.OrderId),
                            ("p_emp_id", OracleDbType.Int32, (object)empId),
                            ("p_note", OracleDbType.Varchar2, noteValue),
                            ("p_signature", OracleDbType.Clob, signatureBase64)
                        },
                        new[] { ("p_stockout_id", OracleDbType.Int32) },
                        transaction);
                    
                    stockOutId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)stockOutOutput["p_stockout_id"]).ToInt32();

                    // 3. Sign stockout
                    string? generatedSignature = OracleHelper.ExecuteClobOutput(
                        conn,
                        "APP.SIGN_STOCKOUT",
                        "p_signature",
                        transaction,
                        ("p_stockout_id", OracleDbType.Int32, stockOutId),
                        ("p_private_key", OracleDbType.Varchar2, dto.PrivateKey ?? ""));
                    
                    string finalsignature = empId + "-" + (generatedSignature ?? "");
                    // 4. Update stockout signature
                    OracleHelper.ExecuteNonQueryWithTransaction(
                        conn,
                        "APP.UPDATE_STOCKOUT_SIGNATURE",
                        transaction,
                        ("p_stockout_id", OracleDbType.Int32, (object)stockOutId),
                        ("p_signature", OracleDbType.Clob, finalsignature));

                    // 5. Create invoice
                    var invoiceOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                        conn,
                        "APP.CREATE_INVOICE",
                        new[] { ("p_stockout_id", OracleDbType.Int32, (object)stockOutId) },
                        new[] { ("p_invoice_id", OracleDbType.Int32) },
                        transaction);
                    
                    invoiceId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)invoiceOutput["p_invoice_id"]).ToInt32();

                    // 6. Sign invoice
                    string? invoiceSignature = OracleHelper.ExecuteClobOutput(
                        conn,
                        "APP.SIGN_INVOICE",
                        "p_signature",
                        transaction,
                        ("p_invoice_id", OracleDbType.Int32, invoiceId),
                        ("p_private_key", OracleDbType.Varchar2, dto.PrivateKey ?? ""));

                    string finalsignature1 = empId + "-" + invoiceSignature;
                    // 7. Update invoice signature
                    OracleHelper.ExecuteNonQueryWithTransaction(
                        conn,
                        "APP.UPDATE_INVOICE_SIGNATURE",
                        transaction,
                        ("p_invoice_id", OracleDbType.Int32, (object)invoiceId),
                        ("p_signature", OracleDbType.Clob, finalsignature1 ?? ""));

                    // Reload data for PDF generation (still in transaction, but we'll commit after PDF is created)
                    var rows = OracleHelper.ExecuteRefCursor(
                        conn,
                        "APP.GET_EXPORT_BY_ID",
                        "cur_out",
                        reader => new
                        {
                            EmpUsername = reader["EmpUsername"]?.ToString(),
                            OutDate = reader["OutDate"] != DBNull.Value ? Convert.ToDateTime(reader["OutDate"]) : (DateTime?)null,
                            Note = reader["Note"]?.ToString(),
                            PartName = reader["PartName"]?.ToString(),
                            Manufacturer = reader["Manufacturer"]?.ToString(),
                            Serial = reader["Serial"]?.ToString(),
                            Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                        },
                        ("p_stockout_id", OracleDbType.Int32, stockOutId));

                    var firstRow = rows.FirstOrDefault();
                    var items = rows.Select(r => new ExportItemDto
                    {
                        PartName = r.PartName,
                        Manufacturer = r.Manufacturer,
                        Serial = r.Serial,
                        Price = r.Price
                    }).ToList();

                    var pdfDto = new ExportStockDto
                    {
                        StockOutId = stockOutId,
                        EmpUsername = firstRow?.EmpUsername ?? "",
                        OutDate = firstRow?.OutDate ?? DateTime.MinValue,
                        Note = firstRow?.Note,
                        Items = items
                    };

                    // Generate PDFs
                    var signedExportPdf = _invoicePdfService.GenerateExportInvoicePdfAndSignWithCertificate(
                        pdfDto, certificateBytes, certificatePassword,
                        cmd => cmd.Parameters.Add("p_stockout_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockOutId,
                        null, null);

                    // Load invoice data and generate invoice PDF
                    var invoiceDto = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                    byte[]? signedInvoicePdf = null;
                    if (invoiceDto != null)
                    {
                        signedInvoicePdf = _invoicePdfService.GenerateInvoicePdfAndSignWithCertificate(
                            invoiceDto, certificateBytes, certificatePassword,
                            cmd => cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId,
                            null, null);
                    }

                    // Commit transaction SAU KHI tạo PDF thành công
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = null;

                    // Encrypt response
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

                    var encryptedResponse = SecurePayloadHelper.EncryptResponse(rsaKeyService, clientId, responseObj);

                    return Ok(encryptedResponse);
                }
                catch (InvalidOperationException ex)
                {
                    transaction?.Rollback();
                    transaction?.Dispose();
                    var encryptedError = SecurePayloadHelper.EncryptResponse(
                        rsaKeyService,
                        clientId,
                        new { Success = false, Message = ex.Message });
                    return Ok(encryptedError);
                }
                catch (Exception ex)
                {
                    transaction?.Rollback();
                    transaction?.Dispose();
                    var encryptedError = SecurePayloadHelper.EncryptResponse(
                        rsaKeyService,
                        clientId,
                        new { Success = false, Message = ex.Message });
                    return Ok(encryptedError);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<EncryptedPayload>.Fail(ex.Message));
            }
        }

        private static bool TryValidateCertificate(string? certificatePfxBase64, string? certificatePassword, out byte[] pfxBytes, out string errorMessage)
        {
            pfxBytes = Array.Empty<byte>();
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(certificatePfxBase64))
            {
                errorMessage = "Certificate PFX is required.";
                return false;
            }

            try
            {
                pfxBytes = Convert.FromBase64String(certificatePfxBase64);
            }
            catch (FormatException)
            {
                errorMessage = "Certificate PFX phải được mã hóa Base64 hợp lệ.";
                return false;
            }

            var password = certificatePassword ?? string.Empty;
            try
            {
                using var cert = new X509Certificate2(
                    pfxBytes,
                    password,
                    X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

                if (!cert.HasPrivateKey)
                {
                    errorMessage = "Certificate PFX không chứa private key hợp lệ.";
                    return false;
                }
            }
            catch (CryptographicException)
            {
                errorMessage = "Không thể mở PFX certificate. Vui lòng kiểm tra lại mật khẩu hoặc định dạng tập tin.";
                return false;
            }

            return true;
        }
    }
}
