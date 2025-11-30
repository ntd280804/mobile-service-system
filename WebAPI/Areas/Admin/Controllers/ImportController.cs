using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Models.Import;
using WebAPI.Helpers;
using WebAPI.Models;
using WebAPI.Models.Security;
using WebAPI.Services;

namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly ControllerHelper _helper;
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly OracleSessionHelper _oracleSessionHelper;
        private readonly PdfService _invoicePdfService;

        public ImportController(
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

        // GET: api/admin/import/getallimport
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllImports()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var result = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_ALL_IMPORTS",
                    "cur_out",
                    reader => new
                    {
                        StockInId = reader["STOCKIN_ID"],
                        EmpUsername = reader["EmpUsername"],
                        OutDate = reader["IN_DATE"],
                        Note = reader["NOTE"]
                    });

                return Ok(result);
            }, "Lỗi khi lấy danh sách phiếu nhập");
        }
        [HttpGet("{stockinId}/verify")]
        [Authorize]
        public async Task<IActionResult> VerifyStockInSignature(int stockinId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                

                // 3. Get signature
                string? signature = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.GET_STOCKIN_SIGNATURE",
                    "p_signature",
                    ("p_stockin_id", OracleDbType.Int32, stockinId));

                if (string.IsNullOrEmpty(signature))
                    return NotFound(new { message = $"Signature của StockIn ID {stockinId} không tồn tại" });

                string empIdStr = signature.Split('-')[0];  // Lấy phần trước dấu "-"
                int empId = int.Parse(empIdStr);            // Chuyển sang int
                signature = signature.Split('-')[1];

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
                    "APP.VERIFY_STOCKIN_SIGNATURE",
                    new[]
                    {
                        ("p_stockin_id", OracleDbType.Int32, (object)stockinId),
                        ("p_public_key", OracleDbType.Varchar2, publicKey),
                        ("p_signature", OracleDbType.Clob, signature)
                    },
                    new[] { ("p_is_valid", OracleDbType.Int32) });

                int isValid = ((Oracle.ManagedDataAccess.Types.OracleDecimal)verifyOutput["p_is_valid"]).ToInt32();

                return Ok(new
                {
                    StockInId = stockinId,
                    IsValid = isValid == 1
                });
            }, "Lỗi xác thực chữ ký phiếu nhập");
        }

        [HttpGet("{stockinId}/invoice")]
        [Authorize]
        public async Task<IActionResult> GetSignedImportInvoicePdf(int stockinId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                byte[]? pdfBytes = OracleHelper.ExecuteBlobOutput(
                    conn,
                    "APP.GET_STOCKIN_PDF",
                    "p_pdf",
                    ("p_stockin_id", OracleDbType.Int32, stockinId));

                if (pdfBytes == null || pdfBytes.Length == 0)
                    return NotFound(new { message = $"Signed PDF for StockIn ID {stockinId} not found or empty" });

                return File(pdfBytes, "application/pdf", $"ImportInvoice_{stockinId}.pdf");
            }, "Lỗi lấy file PDF đã ký phiếu nhập");
        }

        [HttpGet("{stockinid}/details")]
        [Authorize]
        public async Task<IActionResult> GetImportDetails(int stockinid)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var rows = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_IMPORT_BY_ID",
                    "cur_out",
                    reader => new
                    {
                        EmpUsername = reader["EmpUsername"]?.ToString(),
                        InDate = reader["InDate"] != DBNull.Value ? Convert.ToDateTime(reader["InDate"]) : (DateTime?)null,
                        Note = reader["Note"]?.ToString(),
                        PartName = reader["PartName"]?.ToString(),
                        Manufacturer = reader["Manufacturer"]?.ToString(),
                        Serial = reader["Serial"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                    },
                    ("p_stockin_id", OracleDbType.Int32, stockinid));

                if (rows.Count == 0)
                    return NotFound(new { message = $"StockIn ID {stockinid} not found" });

                var firstRow = rows[0];
                var items = rows.Select(r => new ImportItemDto
                {
                    PartName = r.PartName,
                    Manufacturer = r.Manufacturer,
                    Serial = r.Serial,
                    Price = r.Price
                }).ToList();

                var result = new ImportStockDto
                {
                    StockInId = stockinid,
                    EmpUsername = firstRow.EmpUsername ?? "",
                    InDate = firstRow.InDate ?? DateTime.MinValue,
                    Note = firstRow.Note,
                    Items = items
                };

                return Ok(result);
            }, "Lỗi chi tiết phiếu nhập");
        }
        [HttpPost("post")]
        [Authorize]
        public async Task<IActionResult> ImportStockStepByStepWithTransaction([FromBody] ImportStockDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("No items to import");
            
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

            return await _helper.ExecuteWithTransaction(HttpContext, (conn, transaction) =>
            {
                // 1. Get employee ID
                var empIdOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.GET_EMPLOYEE_ID_BY_USERNAME",
                    new[] { ("p_username", OracleDbType.Varchar2, (object)(dto.EmpUsername ?? "")) },
                    new[] { ("p_emp_id", OracleDbType.Int32) });
                
                int empId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)empIdOutput["p_emp_id"]).ToInt32();

                // 2. Create stockin
                string signatureBase64 = "DUMMY_SIGNATURE_FOR_DEMO_PURPOSES";
                var stockInOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.CREATE_STOCKIN",
                    new[]
                    {
                        ("p_emp_id", OracleDbType.Int32, (object)empId),
                        ("p_note", OracleDbType.Varchar2, (object)(dto.Note ?? "")),
                        ("p_signature", OracleDbType.Varchar2, signatureBase64)
                    },
                    new[] { ("p_stockin_id", OracleDbType.Int32) },
                    transaction);
                
                int stockInId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)stockInOutput["p_stockin_id"]).ToInt32();

                // 3. Create items and parts
                foreach (var item in dto.Items)
                {
                    // Create stockin item
                    OracleHelper.ExecuteNonQueryWithOutputs(
                        conn,
                        "APP.CREATE_STOCKIN_ITEM",
                        new[]
                        {
                            ("p_stockin_id", OracleDbType.Int32, (object)stockInId),
                            ("p_part_name", OracleDbType.Varchar2, item.PartName ?? ""),
                            ("p_manufacturer", OracleDbType.Varchar2, item.Manufacturer ?? ""),
                            ("p_serial", OracleDbType.Varchar2, item.Serial ?? ""),
                            ("p_price", OracleDbType.Decimal, (object)item.Price)
                        },
                        new[] { ("p_stockin_item_id", OracleDbType.Int32) },
                        transaction);

                    // Create part
                    OracleHelper.ExecuteNonQueryWithTransaction(
                        conn,
                        "APP.CREATE_PART",
                        transaction,
                        ("p_stockin_id", OracleDbType.Int32, (object)stockInId),
                        ("p_part_name", OracleDbType.Varchar2, item.PartName ?? ""),
                        ("p_manufacturer", OracleDbType.Varchar2, item.Manufacturer ?? ""),
                        ("p_serial", OracleDbType.Varchar2, item.Serial ?? ""),
                        ("p_price", OracleDbType.Decimal, (object)item.Price));
                }

                // 4. Sign stockin
                signatureBase64 = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.SIGN_STOCKIN",
                    "p_signature",
                    transaction,
                    ("p_stockin_id", OracleDbType.Int32, stockInId),
                    ("p_private_key", OracleDbType.Varchar2, "DUMMY")) ?? "";

                // 5. Update signature
                OracleHelper.ExecuteNonQueryWithTransaction(
                    conn,
                    "APP.UPDATE_STOCKIN_SIGNATURE",
                    transaction,
                    ("p_stockin_id", OracleDbType.Int32, (object)stockInId),
                    ("p_signature", OracleDbType.Clob, signatureBase64));

                // Transaction will be committed by ExecuteWithTransaction
                // Now reload data for PDF generation (outside transaction)
                var rows = OracleHelper.ExecuteRefCursor(
                    conn,
                    "APP.GET_IMPORT_BY_ID",
                    "cur_out",
                    reader => new
                    {
                        EmpUsername = reader["EmpUsername"]?.ToString(),
                        InDate = reader["Indate"] != DBNull.Value ? Convert.ToDateTime(reader["Indate"]) : (DateTime?)null,
                        Note = reader["Note"]?.ToString(),
                        PartName = reader["PartName"]?.ToString(),
                        Manufacturer = reader["Manufacturer"]?.ToString(),
                        Serial = reader["Serial"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                    },
                    ("p_stockin_id", OracleDbType.Int32, stockInId));

                var firstRow = rows.FirstOrDefault();
                var itemsSigned = rows.Select(r => new ImportItemDto
                {
                    PartName = r.PartName,
                    Manufacturer = r.Manufacturer,
                    Serial = r.Serial,
                    Price = r.Price
                }).ToList();

                var pdfDto = new ImportStockDto
                {
                    StockInId = stockInId,
                    EmpUsername = firstRow?.EmpUsername ?? dto.EmpUsername ?? "",
                    InDate = firstRow?.InDate ?? DateTime.MinValue,
                    Note = firstRow?.Note ?? dto.Note,
                    Items = itemsSigned
                };

                var signedPdf = _invoicePdfService.GenerateImportInvoicePdfAndSignWithCertificate(
                    pdfDto,
                    certificateBytes,
                    certificatePassword,
                    cmd => cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId,
                    null,
                    null);

                return File(signedPdf, "application/pdf", $"ImportInvoice_{stockInId}.pdf");
            }, "Lỗi nhập kho");
        }

        [HttpPost("create/secure")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> ImportStockSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.EncryptedKeyBlockBase64) || string.IsNullOrWhiteSpace(payload.CipherDataBase64))
                return BadRequest(ApiResponse<EncryptedPayload>.Fail("Invalid encrypted payload"));

            try
            {
                var dto = SecurePayloadHelper.DecryptPayload<ImportStockDto>(rsaKeyService, payload);
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

                // Xử lý import - cần generate PDF trực tiếp để mã hóa response
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
                    int stockInId = 0;

                    // 1. Get employee ID
                    var empIdOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                        conn,
                        "APP.GET_EMPLOYEE_ID_BY_USERNAME",
                        new[] { ("p_username", OracleDbType.Varchar2, (object)(dto.EmpUsername ?? "")) },
                        new[] { ("p_emp_id", OracleDbType.Int32) },
                        transaction);
                    
                    int empId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)empIdOutput["p_emp_id"]).ToInt32();

                    // 2. Create stockin
                    var stockInOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                        conn,
                        "APP.CREATE_STOCKIN",
                        new[]
                        {
                            ("p_emp_id", OracleDbType.Int32, (object)empId),
                            ("p_note", OracleDbType.Varchar2, (object)(dto.Note ?? "")),
                            ("p_signature", OracleDbType.Clob, "DUMMY_SIGNATURE")
                        },
                        new[] { ("p_stockin_id", OracleDbType.Int32) },
                        transaction);
                    
                    stockInId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)stockInOutput["p_stockin_id"]).ToInt32();

                    // 3. Create items and parts
                    foreach (var item in dto.Items ?? new List<ImportItemDto>())
                    {
                        OracleHelper.ExecuteNonQueryWithOutputs(
                            conn,
                            "APP.CREATE_STOCKIN_ITEM",
                            new[]
                            {
                                ("p_stockin_id", OracleDbType.Int32, (object)stockInId),
                                ("p_part_name", OracleDbType.Varchar2, item.PartName ?? ""),
                                ("p_manufacturer", OracleDbType.Varchar2, item.Manufacturer ?? ""),
                                ("p_serial", OracleDbType.Varchar2, item.Serial ?? ""),
                                ("p_price", OracleDbType.Decimal, (object)item.Price)
                            },
                            new[] { ("p_stockin_item_id", OracleDbType.Int32) },
                            transaction);

                        OracleHelper.ExecuteNonQueryWithTransaction(
                            conn,
                            "APP.CREATE_PART",
                            transaction,
                            ("p_stockin_id", OracleDbType.Int32, (object)stockInId),
                            ("p_part_name", OracleDbType.Varchar2, item.PartName ?? ""),
                            ("p_manufacturer", OracleDbType.Varchar2, item.Manufacturer ?? ""),
                            ("p_serial", OracleDbType.Varchar2, item.Serial ?? ""),
                            ("p_price", OracleDbType.Decimal, (object)item.Price));
                    }

                    // 4. Sign stockin
                    string? signature = OracleHelper.ExecuteClobOutput(
                        conn,
                        "APP.SIGN_STOCKIN",
                        "p_signature",
                        transaction,
                        ("p_stockin_id", OracleDbType.Int32, stockInId),
                        ("p_private_key", OracleDbType.Varchar2, dto.PrivateKey ?? ""));
                    
                    string finalsignature = empId + "-" + (signature ?? "");
                    // 5. Update signature
                    OracleHelper.ExecuteNonQueryWithTransaction(
                        conn,
                        "APP.UPDATE_STOCKIN_SIGNATURE",
                        transaction,
                        ("p_stockin_id", OracleDbType.Int32, (object)stockInId),
                        ("p_signature", OracleDbType.Clob, finalsignature));

                    // Reload data for PDF generation (still in transaction, but we'll commit after PDF is created)
                    var rows = OracleHelper.ExecuteRefCursor(
                        conn,
                        "APP.GET_IMPORT_BY_ID",
                        "cur_out",
                        reader => new
                        {
                            EmpUsername = reader["EmpUsername"]?.ToString(),
                            InDate = reader["Indate"] != DBNull.Value ? Convert.ToDateTime(reader["Indate"]) : (DateTime?)null,
                            Note = reader["Note"]?.ToString(),
                            PartName = reader["PartName"]?.ToString(),
                            Manufacturer = reader["Manufacturer"]?.ToString(),
                            Serial = reader["Serial"]?.ToString(),
                            Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                        },
                        ("p_stockin_id", OracleDbType.Int32, stockInId));

                    var firstRow = rows.FirstOrDefault();
                    var itemsSigned = rows.Select(r => new ImportItemDto
                    {
                        PartName = r.PartName,
                        Manufacturer = r.Manufacturer,
                        Serial = r.Serial,
                        Price = r.Price
                    }).ToList();

                    var pdfDto = new ImportStockDto
                    {
                        StockInId = stockInId,
                        EmpUsername = firstRow?.EmpUsername ?? dto.EmpUsername ?? "",
                        InDate = firstRow?.InDate ?? DateTime.MinValue,
                        Note = firstRow?.Note ?? dto.Note,
                        Items = itemsSigned
                    };

                    // Generate PDF with certificate
                    var signedPdf = _invoicePdfService.GenerateImportInvoicePdfAndSignWithCertificate(
                        pdfDto, certificateBytes, certificatePassword,
                        cmd => cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId,
                        null, null);

                    // Commit transaction SAU KHI tạo PDF thành công
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = null;

                    // Encrypt response
                    var responseObj = new
                    {
                        Success = true,
                        Type = "pdf",
                        PdfBase64 = Convert.ToBase64String(signedPdf),
                        FileName = $"ImportInvoice_{stockInId}.pdf"
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
