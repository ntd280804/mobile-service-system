using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models.Order;
using WebApp.Models.Service;
using System.Text.Json;

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

                var response = await _httpClient.GetAsync("api/Common/Order");

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
        public async Task<IActionResult> Details(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Lấy thông tin đơn hàng
                var response = await _httpClient.GetAsync($"api/admin/Order/{id}/details");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Employee", new { area = "Admin" });
                    }

                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không tìm thấy đơn hàng hoặc lỗi API: {response.ReasonPhrase} - {errorMsg}";
                    return RedirectToAction(nameof(Index));
                }

                var order = await response.Content.ReadFromJsonAsync<OrderDto>();
                if (order == null)
                {
                    TempData["Error"] = "Không tìm thấy dữ liệu đơn hàng.";
                    return RedirectToAction(nameof(Index));
                }

                // Lấy danh sách dịch vụ của đơn hàng
                var servicesResponse = await _httpClient.GetAsync($"api/admin/Order/{id}/services");
                List<OrderServiceDto> services = new List<OrderServiceDto>();
                if (servicesResponse.IsSuccessStatusCode)
                {
                    services = await servicesResponse.Content.ReadFromJsonAsync<List<OrderServiceDto>>() ?? new List<OrderServiceDto>();
                }

                // Lấy danh sách parts được gán với order
                try
                {
                    var partsResponse = await _httpClient.GetAsync($"api/admin/Part/{id}/by-order-id");
                    if (partsResponse.IsSuccessStatusCode)
                    {
                        var parts = await partsResponse.Content.ReadFromJsonAsync<List<WebApp.Models.Part.PartDto>>() ?? new List<WebApp.Models.Part.PartDto>();
                        ViewBag.Parts = parts;
                    }
                    else
                    {
                        ViewBag.Parts = new List<WebApp.Models.Part.PartDto>();
                    }
                }
                catch
                {
                    ViewBag.Parts = new List<WebApp.Models.Part.PartDto>();
                }

                // Lấy danh sách parts từ part request
                try
                {
                    var partsRequestResponse = await _httpClient.GetAsync($"api/admin/Part/{id}/by-part-request");
                    if (partsRequestResponse.IsSuccessStatusCode)
                    {
                        var partsRequest = await partsRequestResponse.Content.ReadFromJsonAsync<List<WebApp.Models.Part.PartDto>>() ?? new List<WebApp.Models.Part.PartDto>();
                        ViewBag.PartsRequest = partsRequest;
                    }
                    else
                    {
                        ViewBag.PartsRequest = new List<WebApp.Models.Part.PartDto>();
                    }
                }
                catch
                {
                    ViewBag.PartsRequest = new List<WebApp.Models.Part.PartDto>();
                }

                ViewBag.Services = services;
                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Create()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var username = HttpContext.Session.GetString("Username");

            var model = new CreateOrderRequest
            {
                ReceiverEmpName = username,
                Status = "Đã tiếp nhận",
            };

            try
            {
                // Load customer phones for dropdown
                var phonesResponse = await _httpClient.GetAsync("api/admin/Order/customer-phones");
                if (phonesResponse.IsSuccessStatusCode)
                {
                    var phones = await phonesResponse.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    ViewBag.CustomerPhones = phones.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = p, Text = p }).ToList();
                }

                // Load handler usernames for dropdown
                var usernamesResponse = await _httpClient.GetAsync("api/admin/Order/handler-usernames");
                if (usernamesResponse.IsSuccessStatusCode)
                {
                    var usernames = await usernamesResponse.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    ViewBag.HandlerUsernames = usernames.Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u, Text = u }).ToList();
                }

                // Load services list for dynamic selection
                var servicesResponse = await _httpClient.GetAsync("api/admin/Order/services");
                if (servicesResponse.IsSuccessStatusCode)
                {
                    var services = await servicesResponse.Content.ReadFromJsonAsync<List<ServiceDto>>() ?? new List<ServiceDto>();
                    ViewBag.ServicesJson = JsonSerializer.Serialize(services);
                }
                else
                {
                    ViewBag.ServicesJson = "[]";
                }
            }
            catch (Exception ex)
            {
                // If loading fails, continue with empty dropdowns
                ViewBag.CustomerPhones = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                ViewBag.HandlerUsernames = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                ViewBag.ServicesJson = "[]";
            }

            return View(model);
        }


        // POST: Gửi dữ liệu tạo đơn lên API
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderRequest model)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            // Reload dropdowns for any return to view
            try
            {
                var phonesResponse = await _httpClient.GetAsync("api/admin/Order/customer-phones");
                if (phonesResponse.IsSuccessStatusCode)
                {
                    var phones = await phonesResponse.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    ViewBag.CustomerPhones = phones.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = p, Text = p }).ToList();
                }

                var usernamesResponse = await _httpClient.GetAsync("api/admin/Order/handler-usernames");
                if (usernamesResponse.IsSuccessStatusCode)
                {
                    var usernames = await usernamesResponse.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    ViewBag.HandlerUsernames = usernames.Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u, Text = u }).ToList();
                }

                var servicesResponse = await _httpClient.GetAsync("api/admin/Order/services");
                if (servicesResponse.IsSuccessStatusCode)
                {
                    var services = await servicesResponse.Content.ReadFromJsonAsync<List<ServiceDto>>() ?? new List<ServiceDto>();
                    ViewBag.ServicesJson = JsonSerializer.Serialize(services);
                }
                else
                {
                    ViewBag.ServicesJson = "[]";
                }
            }
            catch
            {
                ViewBag.CustomerPhones = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                ViewBag.HandlerUsernames = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                ViewBag.ServicesJson = "[]";
            }

            if (!ModelState.IsValid)
                return View(model);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.PostAsync($"api/Admin/Order/{orderId}/cancel", null);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    TempData["Message"] = result?["message"] ?? "Hủy đơn hàng thành công.";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    TempData["Error"] = error?["message"] ?? "Không thể hủy đơn hàng.";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    TempData["Error"] = "Lỗi khi hủy đơn hàng: " + response.ReasonPhrase;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

    }
}