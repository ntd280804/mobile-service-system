using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models.Appointment;

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
        public async Task<IActionResult> Index(int page = 1, string phone = null, string appointmentDate = null, string status = null)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            const int pageSize = 10;

            try
            {
                var response = await _httpClient.GetAsync("api/Admin/Appointment/all");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách lịch hẹn: {response.ReasonPhrase} - {errorContent}";
                    var emptyList = WebApp.Models.Common.PaginatedList<AppointmentViewModel>.Create(
                        new List<AppointmentViewModel>(), 
                        page, 
                        pageSize);
                    return View(emptyList);
                }

                var list = await response.Content.ReadFromJsonAsync<List<AppointmentViewModel>>()
                           ?? new List<AppointmentViewModel>();

                // Client-side filtering
                var filtered = list;
                
                if (!string.IsNullOrWhiteSpace(phone))
                    filtered = filtered.Where(a => a.CustomerPhone != null && a.CustomerPhone.Contains(phone)).ToList();
                
                if (!string.IsNullOrWhiteSpace(appointmentDate))
                {
                    if (DateTime.TryParse(appointmentDate, out var searchDate))
                        filtered = filtered.Where(a => a.AppointmentDate.Date == searchDate.Date).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(status))
                    filtered = filtered.Where(a => a.Status != null && a.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

                var paginatedList = WebApp.Models.Common.PaginatedList<AppointmentViewModel>.Create(
                    filtered, 
                    page, 
                    pageSize);

                return View(paginatedList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                var emptyList = WebApp.Models.Common.PaginatedList<AppointmentViewModel>.Create(
                    new List<AppointmentViewModel>(), 
                    page, 
                    pageSize);
                return View(emptyList);
            }
        }

    }
}
