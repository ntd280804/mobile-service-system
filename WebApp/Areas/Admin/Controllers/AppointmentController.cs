using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
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
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Gọi API lấy tất cả appointment
                var response = await _httpClient.GetAsync("api/Admin/Appointment/all");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách lịch hẹn: {response.ReasonPhrase} - {errorContent}";
                    return View(new List<AppointmentViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<AppointmentViewModel>>()
                           ?? new List<AppointmentViewModel>();

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
