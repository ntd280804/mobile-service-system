using Microsoft.AspNetCore.Mvc;
using NuGet.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WebApp.Helpers;
using WebApp.Models.Part;
namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExportController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public ExportController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }

        // DTOs moved to WebApp.Models

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



        // POST: /Admin/Export/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExportStockDto model)
        {
            // Decode URL-encoded private key if present
            if (!string.IsNullOrEmpty(model.PrivateKey))
            {
                try { model.PrivateKey = Uri.UnescapeDataString(model.PrivateKey); } catch { }
            }

            if (string.IsNullOrEmpty(model.PrivateKey))
            {
                TempData["Error"] = "Vui lòng cung cấp private key để hoàn tất và xuất kho.";
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Get current admin username from session headers
                if (!_OracleClientHelper.TryGetSession(out var token, out var username, out var platform, out var sessionId, isAdmin: true))
                {
                    return new RedirectToActionResult("Login", "Employee", new { area = "Admin" });
                }

                // Encrypt payload using server public key
                var keyResp = await _httpClient.GetFromJsonAsync<WebApp.Services.ApiResponse<string>>("api/public/security/server-public-key");
                if (keyResp == null || !keyResp.Success || string.IsNullOrWhiteSpace(keyResp.Data))
                {
                    TempData["Error"] = "Không thể lấy khóa công khai của server.";
                    return RedirectToAction("Index", "Order", new { area = "Admin" });
                }

                var plainDto = new
                {
                    EmpUsername = username,
                    OrderId = (int)model.orderid,
                    PrivateKey = model.PrivateKey
                };

                var json = System.Text.Json.JsonSerializer.Serialize(plainDto);
                var encrypted = WebApp.Helpers.EncryptHelper.HybridEncrypt(json, keyResp.Data);

                var response = await _httpClient.PostAsJsonAsync("api/Admin/export/create-secure", new
                {
                    encryptedKeyBlockBase64 = encrypted.EncryptedKeyBlock,
                    cipherDataBase64 = encrypted.CipherData
                });

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Hoàn tất và xuất kho cho đơn hàng thành công.";
                    return RedirectToAction("Index", "Order", new { area = "Admin" });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    var err = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = "Không thể hoàn tất đơn hàng: " + response.ReasonPhrase + " - " + err;
                    return RedirectToAction("Index", "Order", new { area = "Admin" });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction("Index", "Order", new { area = "Admin" });
            }
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
