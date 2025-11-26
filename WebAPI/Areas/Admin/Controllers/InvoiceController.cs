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
        public IActionResult VerifyInvoiceSignature(int invoiceId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                // 1. lấy chữ ký từ INVOICE
                string signature;
                using (var cmdSig = conn.CreateCommand())
                {
                    cmdSig.CommandText = "APP.GET_INVOICE_SIGNATURE";
                    cmdSig.CommandType = CommandType.StoredProcedure;
                    cmdSig.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId;
                    var pSignature = new OracleParameter("p_signature", OracleDbType.Clob, ParameterDirection.Output);
                    cmdSig.Parameters.Add(pSignature);
                    cmdSig.ExecuteNonQuery();

                    if (pSignature.Value == DBNull.Value || pSignature.Value == null)
                        return NotFound(new { message = $"Signature của Invoice ID {invoiceId} không tồn tại" });

                    var clobSig = (OracleClob)pSignature.Value;
                    signature = clobSig.Value;
                }

                // 2. lấy EMP_ID từ INVOICE
                string empIdStr = signature.Split('-')[0];  // Lấy phần trước dấu "-"
                int empId = int.Parse(empIdStr);            // Chuyển sang int
                signature = signature.Split('-')[1];
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

                    var clobKey = (OracleClob)pPubKey.Value;
                    publicKey = clobKey.Value;
                }

                // 4. verify
                int isValid;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "APP.VERIFY_INVOICE_SIGNATURE";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_invoice_id", OracleDbType.Int32, ParameterDirection.Input).Value = invoiceId;
                    cmd.Parameters.Add("p_public_key", OracleDbType.Varchar2, ParameterDirection.Input).Value = publicKey;
                    cmd.Parameters.Add("p_signature", OracleDbType.Clob, ParameterDirection.Input).Value = signature;
                    var pIsValid = new OracleParameter("p_is_valid", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add(pIsValid);
                    cmd.ExecuteNonQuery();
                    isValid = ((OracleDecimal)pIsValid.Value).ToInt32();
                }

                return Ok(new
                {
                    InvoiceId = invoiceId,
                    IsValid = isValid == 1
                });
            }, "Lỗi khi xác thực hóa đơn");
        }
        [HttpGet]
        [Authorize]
        public IActionResult GetAllInvoices()
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
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

        [HttpGet("{invoiceId}")]
        [Authorize]
        public IActionResult GetInvoiceDetails(int invoiceId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
            {
                var invoice = InvoiceDataHelper.LoadInvoiceData(conn, invoiceId);
                if (invoice == null)
                    return NotFound(new { message = $"Invoice {invoiceId} not found" });

                return Ok(invoice);
            }, "Internal Server Error");
        }

        [HttpGet("{invoiceId}/pdf")]
        [Authorize]
        public IActionResult GetInvoicePdf(int invoiceId)
        {
            return _helper.ExecuteWithConnection(HttpContext, conn =>
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
            }, "Lỗi khi lấy PDF hóa đơn");
        }
    }
}
