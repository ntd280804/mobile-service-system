using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using WebApp.Helpers;
using WebApp.Models.Order;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class OrderController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public OrderController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect, false))
                return redirect;
            var username = HttpContext.Session.GetString("CUsername"); // lấy số điện thoại user hiện tại
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Không tìm thấy số điện thoại trong session";
                return View(new List<OrderDto>());
            }
            try
            {
                var response = await _httpClient.GetAsync($"api/Common/Order");

                if (response.IsSuccessStatusCode)
                {
                    var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>() ?? new List<OrderDto>();
                    return View(orders);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Customer", new { area = "Public" });
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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect, false))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync($"api/Public/Order/{id}/details");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Customer", new { area = "Public" });
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

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}

