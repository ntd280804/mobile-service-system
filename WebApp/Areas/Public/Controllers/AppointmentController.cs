using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models.Appointment;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class AppointmentController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;
        public AppointmentController(IHttpClientFactory httpClientFactory, OracleClientHelper _or)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _or;
        }
        [HttpGet]
        public IActionResult Booking()
        {
            return View(new CreateAppointmentDto());

        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking(CreateAppointmentDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect,false))
                return redirect;

            try
            {
                // Gọi đúng API public
                var response = await _httpClient.PostAsJsonAsync("api/Public/Appointment/Create", dto);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError("", $"Đặt lịch thất bại: {error}");
                    return View(dto);
                }

                // Lấy message trực tiếp từ JSON
                var content = await response.Content.ReadAsStringAsync();
                string message = "Đặt lịch thành công!";
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("message", out var msgElement))
                        message = msgElement.GetString() ?? message;
                }
                catch { /* Ignore nếu JSON không đúng định dạng */ }

                TempData["Success"] = message;
                return RedirectToAction("Booking");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi kết nối API: {ex.Message}");
                return View(dto);
            }
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
                return View(new List<AppointmentViewModel>());
            }

            try
            {
                var response = await _httpClient.GetAsync($"api/Public/Appointment/get-by-phone?phone={username}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải lịch hẹn: {response.ReasonPhrase} - {errorContent}";
                    return View(new List<AppointmentViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<AppointmentViewModel>>() ?? new List<AppointmentViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<AppointmentViewModel>());
            }
        }


    }
}
