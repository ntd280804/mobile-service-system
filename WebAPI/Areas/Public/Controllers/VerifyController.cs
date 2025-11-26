using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using WebAPI.Models;
using WebAPI.Models.Public;
using WebAPI.Services;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class VerifyController : ControllerBase
    {
        private readonly OracleConnectionManager _connManager;

        public VerifyController(OracleConnectionManager connManager)
        {
            _connManager = connManager;
        }

        [HttpPost("verify-invoice")]
        [DisableRequestSizeLimit]
        public async Task<ActionResult<ApiResponse<bool>>> VerifyInvoice([FromForm] VerifyInvoiceRequest request)
        {
            if (request == null || request.File == null || request.File.Length == 0)
            {
                return BadRequest(ApiResponse<bool>.Fail("File không hợp lệ."));
            }

            byte[] uploadedBytes;
            using (var ms = new MemoryStream())
            {
                await request.File.CopyToAsync(ms);
                uploadedBytes = ms.ToArray();
            }

            var invoiceId = request.InvoiceId;
            if (invoiceId <= 0)
            {
                invoiceId = TryExtractInvoiceIdFromPdf(uploadedBytes) ??
                            TryExtractInvoiceIdFromFileName(request.File.FileName) ??
                            0;
            }

            if (invoiceId <= 0)
            {
                return BadRequest(ApiResponse<bool>.Fail("Không xác định được mã hóa đơn từ nội dung PDF."));
            }

            byte[] dbBytes;
            using (var conn = _connManager.CreateDefaultConnection())
            {
                // Set role verify trước khi truy vấn
                using (var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn))
                {
                    setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = "ROLE_VERIFY";
                    setRoleCmd.ExecuteNonQuery();
                }

                using (var cmd = new OracleCommand("SELECT PDF FROM APP.INVOICE WHERE INVOICE_ID = :p_id", conn))
                {
                    cmd.Parameters.Add("p_id", OracleDbType.Decimal).Value = invoiceId;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return NotFound(ApiResponse<bool>.Fail("Không tìm thấy dữ liệu INVOICE."));
                        }
                        using (var blob = reader.GetOracleBlob(0))
                        {
                            dbBytes = blob.Value;
                        }
                    }
                }
            }

            var uploadedHash = ComputeSha256(uploadedBytes);
            var dbHash = ComputeSha256(dbBytes);
            var isMatch = uploadedHash.SequenceEqual(dbHash);

            return Ok(ApiResponse<bool>.Ok(isMatch));
        }

        private static byte[] ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(data);
        }

        private static decimal? TryExtractInvoiceIdFromFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var match = Regex.Match(fileName, @"(\d+)");
            if (match.Success && decimal.TryParse(match.Value, out var value))
            {
                return value;
            }

            return null;
        }

        private static decimal? TryExtractInvoiceIdFromPdf(byte[] pdfBytes)
        {
            try
            {
                using var stream = new MemoryStream(pdfBytes);
                using var document = PdfDocument.Open(stream);

                foreach (var page in document.GetPages())
                {
                    var text = page.Text;
                    var match = Regex.Match(text ?? string.Empty, @"Mã\s*hóa\s*đơn[^0-9]*(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, out var value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }

            return null;
        }
    }
}


