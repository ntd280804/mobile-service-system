using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NuGet.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using WebApp.Helpers;
using WebApp.Models.Part;
using WebApp.Models.Export;
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

        // GET: /Admin/Export/Verifysign/5
        [HttpGet]
        public async Task<IActionResult> Verifysign(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Gọi WebAPI endpoint verify chữ ký
                var response = await _httpClient.GetAsync($"api/admin/export/verifysign/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Xác thực thất bại: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                // Giả sử API trả về JSON { "StockInId": 14, "IsValid": true }
                var result = await response.Content.ReadFromJsonAsync<VerifySignResult>();

                if (result == null)
                {
                    TempData["Error"] = "Không nhận được kết quả từ API";
                    return RedirectToAction(nameof(Index));
                }

                // Thông báo kết quả
                if (result.IsValid)
                {
                    TempData["Success"] = $"Chữ ký StockOut ID {result.StockInId} là hợp lệ ✅";
                }
                else
                {
                    TempData["Error"] = $"Chữ ký StockOut ID {result.StockInId} không hợp lệ ❌";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Admin/Export
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                
                var response = await _httpClient.GetAsync("api/admin/export/getallexport");
                if (!response.IsSuccessStatusCode)
                {
                    // Try to read error details from API response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách export: {response.ReasonPhrase} - {errorContent}";
                    return View(new List<ExportViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<ExportViewModel>>() ?? new List<ExportViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<ExportViewModel>());
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
                var response = await _httpClient.GetAsync($"api/admin/export/details/{id}");
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
                    PrivateKey =model.PrivateKey
                };

                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                var responseObj = await _securityClient.PostEncryptedAndGetEncryptedAsync<object, ExportSecureResponse>(
                    "api/admin/export/create-secure-encrypted", createExportDto);

                // Xử lý response
                if (responseObj != null)
                {
                    if (responseObj.Type == "pdf")
                    {
                        // Ưu tiên trả về Invoice PDF nếu có, nếu không thì Export PDF
                        if (!string.IsNullOrWhiteSpace(responseObj.InvoicePdfBase64))
                        {
                            byte[] pdfBytes = Convert.FromBase64String(responseObj.InvoicePdfBase64);
                            string fileName = responseObj.InvoiceFileName ?? $"Invoice_{responseObj.InvoiceId}.pdf";
                            TempData["Success"] = $"Đã tạo export và invoice thành công. Invoice ID: {responseObj.InvoiceId}";
                            return File(pdfBytes, "application/pdf", fileName);
                        }
                        else if (!string.IsNullOrWhiteSpace(responseObj.ExportPdfBase64))
                        {
                            byte[] pdfBytes = Convert.FromBase64String(responseObj.ExportPdfBase64);
                            string fileName = responseObj.ExportFileName ?? $"ExportInvoice_{responseObj.StockOutId}.pdf";
                            TempData["Success"] = $"Đã tạo export thành công. StockOut ID: {responseObj.StockOutId}";
                            return File(pdfBytes, "application/pdf", fileName);
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
                var response = await _httpClient.GetAsync($"api/admin/export/invoice/{id}");
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
    }
}
