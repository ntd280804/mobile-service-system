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
using WebAPI.Models.Invoice;
using WebAPI.Models;
namespace WebAPI.Areas.Admin.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly ControllerHelper _helper;

        public InvoiceController(
            ControllerHelper helper)
        {
            _helper = helper;
        }
        [HttpGet("{invoiceId}/verify")]
        [Authorize]
        public async Task<IActionResult> VerifyInvoiceSignature(int invoiceId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                // 1. lấy chữ ký từ INVOICE
                string? signature = OracleHelper.ExecuteClobOutput(
                    conn,
                    "APP.GET_INVOICE_SIGNATURE",
                    "p_signature",
                    ("p_invoice_id", OracleDbType.Int32, invoiceId));

                if (string.IsNullOrEmpty(signature))
                        return NotFound(new { message = $"Signature của Invoice ID {invoiceId} không tồn tại" });

                // 2. lấy EMP_ID từ INVOICE
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

                // 3. verify
                var verifyOutput = OracleHelper.ExecuteNonQueryWithOutputs(
                    conn,
                    "APP.VERIFY_INVOICE_SIGNATURE",
                    new[]
                    {
                        ("p_invoice_id", OracleDbType.Int32, (object)invoiceId),
                        ("p_public_key", OracleDbType.Varchar2, publicKey),
                        ("p_signature", OracleDbType.Clob, signature)
                    },
                    new[] { ("p_is_valid", OracleDbType.Int32) });

                int isValid = ((OracleDecimal)verifyOutput["p_is_valid"]).ToInt32();

                return Ok(new
                {
                    InvoiceId = invoiceId,
                    IsValid = isValid == 1
                });
            }, "Lỗi khi xác thực hóa đơn");
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllInvoices()
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var result = OracleHelper.ExecuteRefCursor(conn, "APP.GET_ALL_INVOICES", "cur_out",
                    reader => new
                    {
                        InvoiceId = reader["INVOICE_ID"] != DBNull.Value ? Convert.ToInt32(reader["INVOICE_ID"]) : 0,
                        StockOutId = reader["STOCKOUT_ID"] != DBNull.Value ? Convert.ToInt32(reader["STOCKOUT_ID"]) : 0,
                        CustomerPhone = reader["CUSTOMER_PHONE"]?.ToString(),
                        EmpUsername = reader["EmpUsername"]?.ToString(),
                        InvoiceDate = reader["INVOICE_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["INVOICE_DATE"]) : DateTime.MinValue,
                        TotalAmount = reader["TOTAL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["TOTAL_AMOUNT"]) : 0,
                        Status = reader["STATUS"]?.ToString()
                    });

                return Ok(result);
            }, "Internal Server Error");
        }

        [HttpGet("{invoiceId}/details")]
        [Authorize]
        public async Task<IActionResult> GetInvoiceDetails(int invoiceId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var invoice = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                if (invoice == null)
                    return NotFound(new { message = $"Invoice {invoiceId} not found" });

                return Ok(invoice);
            }, "Internal Server Error");
        }

        [HttpGet("{invoiceId}/pdf")]
        [Authorize]
        public async Task<IActionResult> GetInvoicePdf(int invoiceId)
        {
            return await _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                byte[]? pdfBytes = OracleHelper.ExecuteBlobOutput(
                    conn,
                    "APP.GET_INVOICE_PDF",
                    "p_pdf",
                    ("p_invoice_id", OracleDbType.Int32, invoiceId));

                if (pdfBytes == null || pdfBytes.Length == 0)
                    return NotFound(new { message = $"Signed PDF for Invoice ID {invoiceId} not found or empty" });

                return File(pdfBytes, "application/pdf", $"Invoice_{invoiceId}.pdf");
            }, "Lỗi khi lấy PDF hóa đơn");
        }
    }
}
