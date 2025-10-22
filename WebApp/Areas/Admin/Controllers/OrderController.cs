using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class OrderController : Controller

    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public OrderController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }

        // --- Index: lấy danh sách đơn hàng ---
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {

                var response = await _httpClient.GetAsync("api/Admin/Order");

                if (response.IsSuccessStatusCode)
                {
                    var employees = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
                    return View(employees);
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
                    TempData["Error"] = "Không thể tải danh sách đơn hàng: " + response.ReasonPhrase;
                    return View(new List<OrderDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<OrderDto>());
            }
        }
        [HttpGet]
        public async Task<IActionResult> WarrantyIndex()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Lấy danh sách đơn hàng theo order type "WARRANTY"
                var response = await _httpClient.GetAsync("api/Admin/Order/by-order-type?orderType=WARRANTY");

                if (response.IsSuccessStatusCode)
                {
                    var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
                    return View(orders);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách đơn hàng: " + response.ReasonPhrase;
                    return View(new List<OrderDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<OrderDto>());
            }
        }
        [HttpGet]
        public async Task<IActionResult> RepairIndex()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Lấy danh sách đơn hàng theo order type "REPAIR"
                var response = await _httpClient.GetAsync("api/Admin/Order/by-order-type?orderType=REPAIR");

                if (response.IsSuccessStatusCode)
                {
                    var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
                    return View(orders);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách đơn hàng: " + response.ReasonPhrase;
                    return View(new List<OrderDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<OrderDto>());
            }
        }
        // GET: Form tạo đơn
        [HttpGet]
        public IActionResult Create()
        {
            var username = HttpContext.Session.GetString("Username");

            var model = new CreateOrderRequest
            {
                ReceiverEmpName = username,
                Status = "Đã tiếp nhận",
            };

            return View(model);
        }


        // POST: Gửi dữ liệu tạo đơn lên API
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderRequest model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Order", model);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Tạo đơn hàng thành công.";
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    ModelState.AddModelError(string.Empty, error?["message"] ?? "Lỗi khi tạo đơn hàng.");
                    return View(model);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Lỗi khi tạo đơn hàng: " + response.ReasonPhrase);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Lỗi kết nối API: " + ex.Message);
                return View(model);
            }
        }
        public class OrderDto
        {
            public decimal OrderId { get; set; }
            public string CustomerPhone { get; set; } = string.Empty;
            public string ReceiverEmpName { get; set; } = string.Empty;
            public string HandlerEmpName { get; set; } = string.Empty;
            public string OrderType { get; set; } = string.Empty;
            public DateTime ReceivedDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }
        public class CreateOrderRequest
        {
            public string CustomerPhone { get; set; } = string.Empty;
            public string ReceiverEmpName { get; set; } = string.Empty;
            public string HandlerEmpName { get; set; } = string.Empty;
            public string OrderType { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }
    }
}