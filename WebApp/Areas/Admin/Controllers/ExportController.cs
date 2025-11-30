using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NuGet.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using WebApp.Helpers;
using WebApp.Models.Part;
using WebApp.Services;
namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExportController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;
        private readonly SecurityClient _securityClient;

        public ExportController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper, SecurityClient securityClient)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
            _securityClient = securityClient;
        }
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
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

        [HttpGet]
        public async Task<IActionResult> Verify(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return Json(new { success = false, message = "Vui lòng đăng nhập lại" });

            try
            {
                // Gọi WebAPI endpoint verify chữ ký
                var response = await _httpClient.GetAsync($"api/admin/Export/{id}/verify");
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Xác thực thất bại: {response.ReasonPhrase} - {errorMsg}" });
                }

                // Giả sử API trả về JSON { "StockInId": 14, "IsValid": true }
                var result = await response.Content.ReadFromJsonAsync<VerifySignResult>();

                if (result == null)
                {
                    return Json(new { success = false, message = "Không nhận được kết quả từ API" });
                }

                // Thông báo kết quả
                if (result.IsValid)
                {
                    return Json(new { success = true, message = $"Chữ ký StockOut ID {result.StockInId} là hợp lệ ✅", isValid = true, stockOutId = result.StockInId });
                }
                else
                {
                    return Json(new { success = true, message = $"Chữ ký StockOut ID {result.StockInId} không hợp lệ ❌", isValid = false, stockOutId = result.StockInId });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối API: " + ex.Message });
            }
        }

        // GET: /Admin/Export
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            
            const int pageSize = 10;
            
            try
            {
                var response = await _httpClient.GetAsync("api/admin/Export");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách export: {response.ReasonPhrase} - {errorContent}";
                    var emptyList = WebApp.Models.Common.PaginatedList<ExportViewModel>.Create(
                        new List<ExportViewModel>(), 
                        page, 
                        pageSize);
                    return View(emptyList);
                }

                var list = await response.Content.ReadFromJsonAsync<List<ExportViewModel>>() ?? new List<ExportViewModel>();
                var paginatedList = WebApp.Models.Common.PaginatedList<ExportViewModel>.Create(
                    list, 
                    page, 
                    pageSize);
                return View(paginatedList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                var emptyList = WebApp.Models.Common.PaginatedList<ExportViewModel>.Create(
                    new List<ExportViewModel>(), 
                    page, 
                    pageSize);
                return View(emptyList);
            }
        }

        // GET: /Admin/Export/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Gọi WebAPI để lấy chi tiết export
                var response = await _httpClient.GetAsync($"api/admin/Export/{id}/details");
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không tìm thấy export hoặc lỗi API: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var detail = await response.Content.ReadFromJsonAsync<ExportDetailViewModel>();
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

        // GET: /Admin/Export/Create
        // Export được tạo từ Order, không phải từ form riêng
        [HttpGet]
        public IActionResult Create()
        {
            // Redirect về Order Index vì Export luôn được tạo từ Order
            return RedirectToAction("Index", "Order", new { area = "Admin" });
        }

        // POST: /Admin/Export/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExportStockDto model)
        {
            // Debug: Log model state
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Model validation failed: {errors}. Model: orderid={model?.orderid}, HasPfx={!string.IsNullOrEmpty(model?.CertificatePfxBase64)}, HasPassword={!string.IsNullOrEmpty(model?.CertificatePassword)}";
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }

            if (model == null)
            {
                TempData["Error"] = "Model is null. Please check form submission.";
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }

            if (model.orderid <= 0)
            {
                TempData["Error"] = "Order ID is missing or invalid.";
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }

            if (string.IsNullOrEmpty(model.CertificatePfxBase64) || string.IsNullOrEmpty(model.CertificatePassword))
            {
                TempData["Error"] = "Vui lòng cung cấp PFX certificate và mật khẩu để ký số PDF.";
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }

            try
            {
                // Lấy thông tin từ session
                var username = HttpContext.Session.GetString("Username");
                var platform = HttpContext.Session.GetString("Platform") ?? "WEB";
                if (string.IsNullOrWhiteSpace(username))
                {
                    TempData["Error"] = "Vui lòng đăng nhập trước.";
                    return RedirectToAction("Index", "Order", new { area = "Admin" });
                }
                string client_id = "admin-" + username + platform;

                // Lấy private key từ session
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("PrivateKeyBase64");
                if (string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    TempData["Error"] = "Vui lòng upload private key trước khi sử dụng chức năng này.";
                    return RedirectToAction("Index", "Order", new { area = "Admin" });
                }
                existingPrivateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));

                // Initialize SecurityClient với private key từ session
                await _securityClient.InitializeAsync(
                    HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!,
                    client_id,
                    existingPrivateKeyPem);

                // Set headers cho authenticated request
                var token = HttpContext.Session.GetString("JwtToken");
                var sessionId = HttpContext.Session.GetString("SessionId");
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sessionId))
                {
                    TempData["Error"] = "Vui lòng đăng nhập trước.";
                    return RedirectToAction("Index", "Order", new { area = "Admin" });
                }
                _securityClient.SetHeaders(token, username, platform, sessionId);

                // Tạo DTO để gửi
                var createExportDto = new
                {
                    EmpUsername = username,
                    OrderId = (int)model.orderid,
                    CertificatePfxBase64 = model.CertificatePfxBase64,
                    CertificatePassword = model.CertificatePassword,
                    PrivateKey = model.PrivateKey
                };

                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                var responseObj = await _securityClient.PostEncryptedAndGetEncryptedAsync<object, ExportSecureResponse>(
                    "api/admin/Export/create/secure", createExportDto);

                // Xử lý response
                if (responseObj != null)
                {
                    if (responseObj.Type == "pdf")
                    {
                        // Ưu tiên trả về Invoice PDF nếu có, nếu không thì Export PDF
                        if (!string.IsNullOrWhiteSpace(responseObj.InvoicePdfBase64))
                        {
                            // Lưu PDF vào TempData để download sau khi redirect
                            TempData["PdfBase64"] = responseObj.InvoicePdfBase64;
                            TempData["PdfFileName"] = responseObj.InvoiceFileName ?? $"Invoice_{responseObj.InvoiceId}.pdf";
                            TempData["Success"] = $"Đã tạo export và invoice thành công. Invoice ID: {responseObj.InvoiceId}";
                            TempData["ShouldDownloadPdf"] = "true";
                            return RedirectToAction("Index", "Order", new { area = "Admin" });
                        }
                        else if (!string.IsNullOrWhiteSpace(responseObj.ExportPdfBase64))
                        {
                            // Lưu PDF vào TempData để download sau khi redirect
                            TempData["PdfBase64"] = responseObj.ExportPdfBase64;
                            TempData["PdfFileName"] = responseObj.ExportFileName ?? $"ExportInvoice_{responseObj.StockOutId}.pdf";
                            TempData["Success"] = $"Đã tạo export thành công. StockOut ID: {responseObj.StockOutId}";
                            TempData["ShouldDownloadPdf"] = "true";
                            return RedirectToAction("Index", "Order", new { area = "Admin" });
                        }
                    }
                    
                    if (responseObj.Success)
                    {
                        TempData["Success"] = "Export thành công";
                        return RedirectToAction("Index", "Order", new { area = "Admin" });
                    }
                }

                // Nếu có error message
                TempData["Error"] = $"Không thể hoàn tất đơn hàng: {responseObj?.Message ?? "Response không hợp lệ"}";
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }
        }
        
        // GET: /Admin/Export/Invoice/5
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/Export/{id}/invoice");
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Tải hóa đơn thất bại: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var stream = await response.Content.ReadAsStreamAsync();
                var fileName = $"ExportInvoice_{id}.pdf";
                return File(stream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // DTOs for encrypted response
        public class ExportSecureResponse
        {
            public bool Success { get; set; }
            public string? Type { get; set; } // "pdf" or null
            public string? ExportPdfBase64 { get; set; }
            public string? ExportFileName { get; set; }
            public string? InvoicePdfBase64 { get; set; }
            public string? InvoiceFileName { get; set; }
            public int? InvoiceId { get; set; }
            public int? StockOutId { get; set; }
            public string? Message { get; set; }
        }
    }
}
