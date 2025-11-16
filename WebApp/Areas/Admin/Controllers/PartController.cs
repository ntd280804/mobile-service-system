using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using WebApp.Helpers;
using WebApp.Models.Part;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PartController : Controller

    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public PartController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }

        // ĐÃ SỬA: Chuyển Field sang Property (có { get; set; }) và sử dụng kiểu Nullable (?) cho OrderId và Price.
        // Price được đổi từ long sang decimal? để khớp với kiểu trả về từ API (decimal).
        

        // --- Index: lấy danh sách nhân viên ---
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {

                var response = await _httpClient.GetAsync("api/Admin/Part");

                if (response.IsSuccessStatusCode)
                {
                    // Lỗi cũ: var employees = await response.Content.ReadFromJsonAsync<List<PartDto>>();
                    // Đã sửa: Sử dụng tên biến 'parts' để phù hợp với ngữ cảnh
                    var parts = await response.Content.ReadFromJsonAsync<List<PartDto>>();
                    return View(parts);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Session Oracle bị kill → redirect login
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách linh kiện: " + response.ReasonPhrase;
                    return View(new List<PartDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<PartDto>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(string serial)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/admin/part/details/{serial}");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Employee", new { area = "Admin" });
                    }

                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không tìm thấy linh kiện hoặc lỗi API: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var part = await response.Content.ReadFromJsonAsync<PartDto>();
                if (part == null)
                {
                    TempData["Error"] = "Không tìm thấy dữ liệu linh kiện.";
                    return RedirectToAction(nameof(Index));
                }

                // Lấy danh sách parts từ part request nếu part có OrderId
                if (part.OrderId.HasValue)
                {
                    try
                    {
                        var partsRequestResponse = await _httpClient.GetAsync($"api/admin/part/by-part-request/{part.OrderId.Value}");
                        if (partsRequestResponse.IsSuccessStatusCode)
                        {
                            var partsRequest = await partsRequestResponse.Content.ReadFromJsonAsync<List<PartDto>>() ?? new List<PartDto>();
                            ViewBag.PartsRequest = partsRequest;
                        }
                        else
                        {
                            ViewBag.PartsRequest = new List<PartDto>();
                        }
                    }
                    catch
                    {
                        ViewBag.PartsRequest = new List<PartDto>();
                    }
                }
                else
                {
                    ViewBag.PartsRequest = new List<PartDto>();
                }

                return View(part);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
