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
        public async Task<IActionResult> Index()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync("api/admin/invoice");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách hóa đơn: {response.ReasonPhrase} - {error}";
                    return View(new List<InvoiceSummaryViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<InvoiceSummaryViewModel>>() ?? new List<InvoiceSummaryViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<InvoiceSummaryViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/invoice/{id}");
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
                var response = await _httpClient.GetAsync($"api/admin/invoice/{id}/pdf");
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

