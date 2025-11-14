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
    public class ImportController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public ImportController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }

        // DTOs moved to WebApp.Models

        // GET: /Admin/Import
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                
                var response = await _httpClient.GetAsync("api/admin/import/getallimport");
                if (!response.IsSuccessStatusCode)
                {
                    // Try to read error details from API response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách import: {response.ReasonPhrase} - {errorContent}";
                    return View(new List<ImportViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<ImportViewModel>>() ?? new List<ImportViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<ImportViewModel>());
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
                var response = await _httpClient.GetAsync($"api/admin/import/details/{id}");
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
                return View(model);
            }

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                // Encrypt payload (includes PrivateKey)
                var keyResp = await _httpClient.GetFromJsonAsync<WebApp.Services.ApiResponse<string>>("api/public/security/server-public-key");
                if (keyResp == null || !keyResp.Success || string.IsNullOrWhiteSpace(keyResp.Data))
                {
                    TempData["Error"] = "Không thể lấy khóa công khai của server.";
                    return View(model);
                }
                var json = System.Text.Json.JsonSerializer.Serialize(model);
                var encrypted = WebApp.Helpers.EncryptHelper.HybridEncrypt(json, keyResp.Data);

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/admin/import/post-secure");
                requestMessage.Content = JsonContent.Create(new
                {
                    encryptedKeyBlockBase64 = encrypted.EncryptedKeyBlock,
                    cipherDataBase64 = encrypted.CipherData
                });
                // Prefer receiving PDF if API returns it
                requestMessage.Headers.Accept.Clear();
                requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
                requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        var stream = await response.Content.ReadAsStreamAsync();
                        // Try to parse stockInId from Content-Disposition filename if present; otherwise fallback to timestamp
                        var fileName = "ImportInvoice.pdf";
                        if (response.Content.Headers.ContentDisposition != null && !string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition.FileNameStar))
                            fileName = response.Content.Headers.ContentDisposition.FileNameStar;
                        else if (response.Content.Headers.ContentDisposition != null && !string.IsNullOrWhiteSpace(response.Content.Headers.ContentDisposition.FileName))
                            fileName = response.Content.Headers.ContentDisposition.FileName.Trim('\"');
                        return File(stream, "application/pdf", fileName);
                    }
                    else
                    {
                        TempData["Success"] = "Import thành công";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Import thất bại: {msg}";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(model);
            }
        }
        // GET: /Admin/Import/Verifysign/5
        [HttpGet]
        public async Task<IActionResult> Verifysign(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Gọi WebAPI endpoint verify chữ ký
                var response = await _httpClient.GetAsync($"api/admin/import/verifysign/{id}");
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
                    TempData["Success"] = $"Chữ ký StockIn ID {result.StockInId} là hợp lệ ✅";
                }
                else
                {
                    TempData["Error"] = $"Chữ ký StockIn ID {result.StockInId} không hợp lệ ❌";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Admin/Import/Invoice/5
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/import/invoice/{id}");
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

        // POST: /Admin/Import/Invoice/5 (send body to API GET endpoint)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Invoice(int id, string PrivateKey)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var decodedKey = string.IsNullOrEmpty(PrivateKey) ? "" : Uri.UnescapeDataString(PrivateKey);
                var req = new HttpRequestMessage(HttpMethod.Get, $"api/admin/import/invoice/{id}")
                {
                    Content = JsonContent.Create(new
                    {
                        EmpUsername = "",
                        StockinId = id,
                        PrivateKey = decodedKey
                    })
                };

                var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorMsg = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Tải hóa đơn thất bại: {resp.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var stream = await resp.Content.ReadAsStreamAsync();
                var fileName = $"ImportInvoice_{id}.pdf";
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
