using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ImportController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://10.147.20.199:5131/") // đổi URL WebAPI
        };

        // DTOs
        public class ImportItemDto
        {
            public string PartName { get; set; }
            public string Manufacturer { get; set; }
            public string Serial { get; set; }
        }

        public class ImportStockDto
        {
            public int EmpId { get; set; }
            public string Note { get; set; }
            public List<ImportItemDto> Items { get; set; } = new();
        }

        public class ImportViewModel
        {
            public int StockInId { get; set; }
            public int EmpId { get; set; }
            public DateTime InDate { get; set; }
            public string Note { get; set; }
            public ImportItemDto StockInItem { get; set; }
        }

        public class ImportDetailViewModel
        {
            public int StockInId { get; set; }
            public int EmpId { get; set; }
            public DateTime InDate { get; set; }
            public string Note { get; set; }
            public List<ImportItemDto> Items { get; set; } = new();
        }

        // GET: /Admin/Import
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            SetOracleHeadersFromSession();
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
            SetOracleHeadersFromSession();
            try
            {
                var response = await _httpClient.GetAsync($"api/admin/import/getimportid/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Không tìm thấy import";
                    return RedirectToAction(nameof(Index));
                }

                var detail = await response.Content.ReadFromJsonAsync<ImportDetailViewModel>();
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

            SetOracleHeadersFromSession();
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/admin/import/post", model);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Import thành công";
                    return RedirectToAction(nameof(Index));
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

        // Helper: set header Oracle từ session
        private void SetOracleHeadersFromSession()
        {
            var username = HttpContext.Session.GetString("Username");
            var platform = HttpContext.Session.GetString("Platform");
            var sessionId = HttpContext.Session.GetString("SessionId");

            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

            if (!string.IsNullOrEmpty(username))
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
            if (!string.IsNullOrEmpty(platform))
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
            if (!string.IsNullOrEmpty(sessionId))
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);
        }
    }
}
