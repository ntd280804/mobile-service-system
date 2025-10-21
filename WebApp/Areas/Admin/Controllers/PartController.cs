using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using WebApp.Helpers;

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
        public class PartDto
        {
            public decimal PartId { get; set; }
            public string Name { get; set; }
            public string Manufacturer { get; set; }
            public string Serial { get; set; }
            public byte[] QRImage { get; set; }
            public string Status { get; set; }
            public decimal StockinItemId { get; set; }

            // Xử lý giá trị NULL: Dùng decimal? thay vì decimal
            public decimal? OrderId { get; set; }

            // Xử lý giá trị NULL: Dùng decimal? thay vì long/decimal
            public decimal? Price { get; set; }
        }

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
    }
}
