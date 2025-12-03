using Microsoft.AspNetCore.Mvc;
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
    public class ImportController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;
        private readonly SecurityClient _securityClient;

        public ImportController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper, SecurityClient securityClient)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
            _securityClient = securityClient;
        }

        [HttpGet]
        public async Task<IActionResult> Verify(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return Json(new { success = false, message = "Vui lòng đăng nhập lại" });

            try
            {
                // Gọi WebAPI endpoint verify chữ ký
                var response = await _httpClient.GetAsync($"api/admin/Import/{id}/verify");
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
                    return Json(new { success = true, message = $"Chữ ký StockIn ID {result.StockInId} là hợp lệ ✅", isValid = true, stockInId = result.StockInId });
                }
                else
                {
                    return Json(new { success = true, message = $"Chữ ký StockIn ID {result.StockInId} không hợp lệ ❌", isValid = false, stockInId = result.StockInId });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối API: " + ex.Message });
            }
        }

        // GET: /Admin/Import
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            
            const int pageSize = 10;
            
            try
            {
                var response = await _httpClient.GetAsync("api/admin/import");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách import: {response.ReasonPhrase} - {errorContent}";
                    var emptyList = WebApp.Models.Common.PaginatedList<ImportViewModel>.Create(
                        new List<ImportViewModel>(), 
                        page, 
                        pageSize);
                    return View(emptyList);
                }

                var list = await response.Content.ReadFromJsonAsync<List<ImportViewModel>>() ?? new List<ImportViewModel>();
                var paginatedList = WebApp.Models.Common.PaginatedList<ImportViewModel>.Create(
                    list, 
                    page, 
                    pageSize);
                return View(paginatedList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                var emptyList = WebApp.Models.Common.PaginatedList<ImportViewModel>.Create(
                    new List<ImportViewModel>(), 
                    page, 
                    pageSize);
                return View(emptyList);
            }
        }

        // GET: /Admin/Import/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Gọi WebAPI để lấy chi tiết import
                var response = await _httpClient.GetAsync($"api/admin/import/{id}/details");
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không tìm thấy import hoặc lỗi API: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var detail = await response.Content.ReadFromJsonAsync<ImportDetailViewModel>();
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


        // GET: /Admin/Import/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new ImportStockDto { Items = new List<ImportItemDto> { new ImportItemDto() } });
        }

        // POST: /Admin/Import/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ImportStockDto model)
        {
            if (model.Items == null || !model.Items.Any())
            {
                TempData["Error"] = "Vui lòng nhập ít nhất 1 item";
                return RedirectToAction(nameof(Index));
            }

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                // Lấy thông tin từ session
                var username = HttpContext.Session.GetString("Username");
                var platform = HttpContext.Session.GetString("Platform") ?? "WEB";
                if (string.IsNullOrWhiteSpace(username))
                {
                    TempData["Error"] = "Vui lòng đăng nhập trước.";
                    return RedirectToAction(nameof(Index));
                }
                string client_id = "admin-"+username + platform;

                // Lấy private key từ session
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("PrivateKeyBase64");
                model.PrivateKey = HttpContext.Session.GetString("PrivateKeyBase");
                if (string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    TempData["Error"] = "Vui lòng upload private key trước khi sử dụng chức năng này.";
                    return RedirectToAction(nameof(Index));
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
                    return RedirectToAction(nameof(Index));
                }
                _securityClient.SetHeaders(token, username, platform, sessionId);

                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                var responseObj = await _securityClient.PostEncryptedAndGetEncryptedAsync<ImportStockDto, ImportSecureResponse>(
                    "api/admin/import/create/secure", model);

                // Xử lý response
                if (responseObj != null)
                {
                    if (responseObj.Type == "pdf" && !string.IsNullOrWhiteSpace(responseObj.PdfBase64))
                    {
                        // Lưu PDF vào TempData để download sau khi redirect
                        TempData["PdfBase64"] = responseObj.PdfBase64;
                        TempData["PdfFileName"] = responseObj.FileName ?? "ImportInvoice.pdf";
                        TempData["Success"] = "Import thành công";
                        TempData["ShouldDownloadPdf"] = "true";
                        return RedirectToAction(nameof(Index));
                    }
                    else if (responseObj.Success)
                    {
                        TempData["Success"] = "Import thành công";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Nếu có error message
                TempData["Error"] = $"Import thất bại: {responseObj?.Message ?? "Response không hợp lệ"}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
        // GET: /Admin/Import/Verifysign/5
        
        // GET: /Admin/Import/Invoice/5
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/import/{id}/invoice");
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Tải hóa đơn thất bại: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var stream = await response.Content.ReadAsStreamAsync();
                var fileName = $"ImportInvoice_{id}.pdf";
                return File(stream, "application/pdf", fileName);
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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/Import/{id}/invoice");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải PDF hóa đơn: {response.ReasonPhrase} - {error}";
                    return RedirectToAction(nameof(Index));
                }

                var stream = await response.Content.ReadAsStreamAsync();
                var fileName = $"ImportInvoice_{id}.pdf";
                return File(stream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }


        // GET: /Admin/Import/Invoice/5 - PDF đã được lưu trong DB, không cần certificate

        // DTOs for encrypted response
        public class ImportSecureResponse
        {
            public bool Success { get; set; }
            public string? Type { get; set; } // "pdf" or null
            public string? PdfBase64 { get; set; }
            public string? FileName { get; set; }
            public string? Message { get; set; }
        }
    }
}
