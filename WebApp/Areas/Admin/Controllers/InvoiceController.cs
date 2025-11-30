using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models.Invoice;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class InvoiceController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _oracleClientHelper;

        public InvoiceController(IHttpClientFactory httpClientFactory, OracleClientHelper oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _oracleClientHelper = oracleClientHelper;
        }
        [HttpGet]
        public async Task<IActionResult> Verify(int id)
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return Json(new { success = false, message = "Vui lòng đăng nhập lại" });

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/Invoice/{id}/verify");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Xác thực thất bại: {response.ReasonPhrase} - {error}" });
                }

                var result = await response.Content.ReadFromJsonAsync<VerifyInvoiceResultViewModel>();
                if (result == null)
                {
                    return Json(new { success = false, message = "Không nhận được kết quả xác thực" });
                }
                
                if (result.IsValid)
                {
                    return Json(new { success = true, message = $"Chữ ký hóa đơn #{result.InvoiceId} hợp lệ ✅", isValid = true, invoiceId = result.InvoiceId });
                }
                else
                {
                    return Json(new { success = true, message = $"Chữ ký hóa đơn #{result.InvoiceId} không hợp lệ ❌", isValid = false, invoiceId = result.InvoiceId });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối API: " + ex.Message });
            }
        }
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string invoiceId = null, string customerPhone = null, string dateFrom = null, string dateTo = null)
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            const int pageSize = 10;

            try
            {
                var response = await _httpClient.GetAsync("api/admin/invoice");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách hóa đơn: {response.ReasonPhrase} - {error}";
                    var emptyList = WebApp.Models.Common.PaginatedList<InvoiceSummaryViewModel>.Create(
                        new List<InvoiceSummaryViewModel>(), 
                        page, 
                        pageSize);
                    return View(emptyList);
                }

                var list = await response.Content.ReadFromJsonAsync<List<InvoiceSummaryViewModel>>() ?? new List<InvoiceSummaryViewModel>();
                
                // Client-side filtering
                var filtered = list;
                
                if (!string.IsNullOrWhiteSpace(invoiceId) && int.TryParse(invoiceId, out var invId))
                    filtered = filtered.Where(i => i.InvoiceId == invId).ToList();
                
                if (!string.IsNullOrWhiteSpace(customerPhone))
                    filtered = filtered.Where(i => i.CustomerPhone != null && i.CustomerPhone.Contains(customerPhone)).ToList();
                
                if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
                    filtered = filtered.Where(i => i.InvoiceDate.Date >= fromDate.Date).ToList();
                
                if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var toDate))
                    filtered = filtered.Where(i => i.InvoiceDate.Date <= toDate.Date).ToList();
                
                var paginatedList = WebApp.Models.Common.PaginatedList<InvoiceSummaryViewModel>.Create(
                    filtered, 
                    page, 
                    pageSize);
                return View(paginatedList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                var emptyList = WebApp.Models.Common.PaginatedList<InvoiceSummaryViewModel>.Create(
                    new List<InvoiceSummaryViewModel>(), 
                    page, 
                    pageSize);
                return View(emptyList);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/Invoice/{id}/details");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không tìm thấy hóa đơn: {response.ReasonPhrase} - {error}";
                    return RedirectToAction(nameof(Index));
                }

                var detail = await response.Content.ReadFromJsonAsync<InvoiceDetailViewModel>();
                if (detail == null)
                {
                    TempData["Error"] = "Không nhận được dữ liệu chi tiết từ API";
                    return RedirectToAction(nameof(Index));
                }

                return View(detail);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/Invoice/{id}/pdf");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải PDF hóa đơn: {response.ReasonPhrase} - {error}";
                    return RedirectToAction(nameof(Index));
                }

                var stream = await response.Content.ReadAsStreamAsync();
                var fileName = $"Invoice_{id}.pdf";
                return File(stream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        
    }
}

