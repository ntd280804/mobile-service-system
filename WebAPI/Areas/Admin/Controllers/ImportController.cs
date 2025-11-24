using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
                        OutDate = reader["IN_DATE"],
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
        /// <summary>
        /// Returns the previously saved signed PDF BLOB for a StockIn, if present.
        /// </summary>
        [HttpGet("invoice/{stockinId}")]
        [Authorize]
        public IActionResult GetSignedImportInvoicePdf(int stockinId)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {
                byte[] pdfBytes = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_STOCKIN_PDF";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinId;
                    var pPdf = new OracleParameter("p_pdf", OracleDbType.Blob, ParameterDirection.Output);
                    cmd.Parameters.Add(pPdf);

                    cmd.ExecuteNonQuery();

                    if (pPdf.Value == null || pPdf.Value == DBNull.Value)
                    {
                        return NotFound(new { message = $"Signed PDF for StockIn ID {stockinId} not found" });
                    }

                    using (var blob = (Oracle.ManagedDataAccess.Types.OracleBlob)pPdf.Value)
                    {
                        pdfBytes = blob?.Value;
                    }
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return NotFound(new { message = $"Signed PDF for StockIn ID {stockinId} is empty" });
                }

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

        [HttpGet("details/{stockinid}")]
        [Authorize]
        public IActionResult GetImportDetails(int stockinid)
        {
            var conn = _oracleSessionHelper.GetConnectionOrUnauthorized(HttpContext, _connManager, out var unauthorized);
            if (conn == null) return unauthorized;

            try
            {

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "APP.GET_IMPORT_BY_ID";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockinid;
                var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(outputCursor);

                var items = new List<ImportItemDto>();
                string? empUsername = null;
                DateTime? inDate = null;
                string note = null;

                using var reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                    return NotFound(new { message = $"StockIn ID {stockinid} not found" });

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
                    StockInId = stockinid,
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
            bool hasProvidedPfx = !string.IsNullOrWhiteSpace(dto.CertificatePfxBase64) && !string.IsNullOrWhiteSpace(dto.CertificatePassword);
            if (!hasProvidedPfx)
            {
                throw new InvalidOperationException(
                    "Không thể tạo PFX hợp lệ khi chỉ có private key. Vui lòng cung cấp PFX do CA cấp.");
            }
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
                        cmdPart.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                        cmdPart.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = partName;
                        cmdPart.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = manufacturer;
                        cmdPart.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = serial;
                        cmdPart.Parameters.Add("p_price", OracleDbType.Decimal, ParameterDirection.Input).Value = item.Price; // <-- thêm price


                        cmdPart.ExecuteNonQuery();
                    }

                }
                using (var cmdSign = conn.CreateCommand())
                {
                    cmdSign.Transaction = transaction;
                    cmdSign.CommandText = "APP.SIGN_STOCKIN";
                    cmdSign.CommandType = CommandType.StoredProcedure;

                    cmdSign.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                    cmdSign.Parameters.Add("p_private_key", OracleDbType.Varchar2, ParameterDirection.Input).Value = "DUMMY";

                    var outSignature = new OracleParameter("p_signature", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
                    cmdSign.Parameters.Add(outSignature);

                    cmdSign.ExecuteNonQuery();
                    signatureBase64 = outSignature.Value.ToString();
                }
                using (var cmdUpdateSig = conn.CreateCommand())
                {
                    cmdUpdateSig.Transaction = transaction;
                    cmdUpdateSig.CommandText = "APP.UPDATE_STOCKIN_SIGNATURE";
                    cmdUpdateSig.CommandType = CommandType.StoredProcedure;

                    cmdUpdateSig.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                    cmdUpdateSig.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = signatureBase64;

                    cmdUpdateSig.ExecuteNonQuery();
                }
                // Ký số nghiệp vụ không cần thiết khi dùng PFX certificate để ký PDF
                transaction.Commit();
                // 6) Generate PDF and sign with PFX certificate
                
                // Reload data to render PDF
                var itemsSigned = new List<ImportItemDto>();
                string? empUsernameSigned = dto.EmpUsername;
                DateTime? inDateSigned = null;
                string noteSigned = dto.Note ?? "";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.GET_IMPORT_BY_ID";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                    var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                    cmd.Parameters.Add(outputCursor);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (inDateSigned == null)
                        {
                            empUsernameSigned ??= Convert.ToString(reader["EmpUsername"]);
                            inDateSigned = Convert.ToDateTime(reader["Indate"]);
                            noteSigned = reader["Note"]?.ToString();
                        }
                        itemsSigned.Add(new ImportItemDto
                        {
                            PartName = reader["PartName"]?.ToString(),
                            Manufacturer = reader["Manufacturer"]?.ToString(),
                            Serial = reader["Serial"]?.ToString(),
                            Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                        });
                    }
                }
                var pdfDto = new ImportStockDto
                {
                    StockInId = stockInId,
                    EmpUsername = empUsernameSigned ?? string.Empty,
                    InDate = inDateSigned ?? DateTime.MinValue,
                    Note = noteSigned,
                    Items = itemsSigned
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

                var signedPdf = _invoicePdfService.GenerateImportInvoicePdfAndSignWithCertificate(
                    pdfDto,
                    pfxBytes,
                    pfxPassword,
                    cmd =>
                    {
                        cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                    },
                    null,
                    null
                );
                var fileNameOut = $"ImportInvoice_{stockInId}.pdf";
                return File(signedPdf, "application/pdf", fileNameOut);
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

        [HttpPost("post-secure-encrypted")]
        [Authorize]
        public ActionResult<ApiResponse<EncryptedPayload>> ImportStockSecureEncrypted([FromServices] RsaKeyService rsaKeyService, [FromBody] EncryptedPayload payload)
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

                var dto = System.Text.Json.JsonSerializer.Deserialize<ImportStockDto>(plaintext);
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

                // Xử lý import - cần generate PDF trực tiếp để mã hóa response
                // Vì FileResult không thể serialize, ta cần generate PDF trực tiếp
                try
                {
                    // Gọi lại logic để lấy PDF bytes
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

                    // Execute import và get stockInId
                    int stockInId = 0;
                    using var transaction = conn.BeginTransaction();
                    try
                    {
                        int empId;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "APP.GET_EMPLOYEE_ID_BY_USERNAME";
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("p_username", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.EmpUsername;
                            var pEmpId = new OracleParameter("p_emp_id", OracleDbType.Int32, ParameterDirection.Output);
                            cmd.Parameters.Add(pEmpId);
                            cmd.ExecuteNonQuery();
                            empId = Convert.ToInt32(pEmpId.Value.ToString());
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "APP.CREATE_STOCKIN";
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("p_emp_id", OracleDbType.Int32, ParameterDirection.Input).Value = empId;
                            cmd.Parameters.Add("p_note", OracleDbType.Varchar2, ParameterDirection.Input).Value = dto.Note ?? "";
                            cmd.Parameters.Add("p_signature", OracleDbType.Varchar2, ParameterDirection.Input).Value = "DUMMY_SIGNATURE";
                            var outStockInId = new OracleParameter("p_stockin_id", OracleDbType.Int32, ParameterDirection.Output);
                            cmd.Parameters.Add(outStockInId);
                            cmd.ExecuteNonQuery();
                            stockInId = Convert.ToInt32(outStockInId.Value.ToString());
                        }

                        foreach (var item in dto.Items)
                        {
                            using (var cmdItem = conn.CreateCommand())
                            {
                                cmdItem.Transaction = transaction;
                                cmdItem.CommandText = "APP.CREATE_STOCKIN_ITEM";
                                cmdItem.CommandType = CommandType.StoredProcedure;
                                cmdItem.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                                cmdItem.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = item.PartName;
                                cmdItem.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = item.Manufacturer ?? "";
                                cmdItem.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = item.Serial;
                                cmdItem.Parameters.Add("p_price", OracleDbType.Decimal, ParameterDirection.Input).Value = item.Price;
                                var outStockInItemId = new OracleParameter("p_stockin_item_id", OracleDbType.Int32, ParameterDirection.Output);
                                cmdItem.Parameters.Add(outStockInItemId);
                                cmdItem.ExecuteNonQuery();
                            }

                            using (var cmdPart = conn.CreateCommand())
                            {
                                cmdPart.Transaction = transaction;
                                cmdPart.CommandText = "APP.CREATE_PART";
                                cmdPart.CommandType = CommandType.StoredProcedure;
                                cmdPart.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                                cmdPart.Parameters.Add("p_part_name", OracleDbType.Varchar2, ParameterDirection.Input).Value = item.PartName;
                                cmdPart.Parameters.Add("p_manufacturer", OracleDbType.Varchar2, ParameterDirection.Input).Value = item.Manufacturer ?? "";
                                cmdPart.Parameters.Add("p_serial", OracleDbType.Varchar2, ParameterDirection.Input).Value = item.Serial;
                                cmdPart.Parameters.Add("p_price", OracleDbType.Decimal, ParameterDirection.Input).Value = item.Price;
                                cmdPart.ExecuteNonQuery();
                            }
                        }
                        
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }

                    // Generate PDF
                    var itemsSigned = new List<ImportItemDto>();
                    string? empUsernameSigned = dto.EmpUsername;
                    DateTime? inDateSigned = null;
                    string noteSigned = dto.Note ?? "";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "APP.GET_IMPORT_BY_ID";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId;
                        var outputCursor = new OracleParameter("cur_out", OracleDbType.RefCursor, ParameterDirection.Output);
                        cmd.Parameters.Add(outputCursor);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            if (inDateSigned == null)
                            {
                                empUsernameSigned ??= Convert.ToString(reader["EmpUsername"]);
                                inDateSigned = Convert.ToDateTime(reader["Indate"]);
                                noteSigned = reader["Note"]?.ToString() ?? "";
                            }
                            itemsSigned.Add(new ImportItemDto
                            {
                                PartName = reader["PartName"]?.ToString(),
                                Manufacturer = reader["Manufacturer"]?.ToString(),
                                Serial = reader["Serial"]?.ToString(),
                                Price = reader["Price"] != DBNull.Value ? Convert.ToInt64(reader["Price"]) : 0
                            });
                        }
                    }

                    var pdfDto = new ImportStockDto
                    {
                        StockInId = stockInId,
                        EmpUsername = empUsernameSigned ?? string.Empty,
                        InDate = inDateSigned ?? DateTime.MinValue,
                        Note = noteSigned,
                        Items = itemsSigned
                    };

                    byte[] pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
                    string pfxPassword = dto.CertificatePassword;
                    var signedPdf = _invoicePdfService.GenerateImportInvoicePdfAndSignWithCertificate(
                        pdfDto, pfxBytes, pfxPassword,
                        cmd => cmd.Parameters.Add("p_stockin_id", OracleDbType.Int32, ParameterDirection.Input).Value = stockInId,
                        null, null);

                    // Tạo response object chứa PDF base64 và filename
                    var responseObj = new
                    {
                        Success = true,
                        Type = "pdf",
                        PdfBase64 = Convert.ToBase64String(signedPdf),
                        FileName = $"ImportInvoice_{stockInId}.pdf"
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
        public List<ImportItemDto> Items { get; set; }
        // Required: PFX certificate từ CA để ký PDF
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
        }
}
