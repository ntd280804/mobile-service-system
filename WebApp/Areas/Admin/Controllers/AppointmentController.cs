using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AppointmentController : Controller
    {
        private readonly HttpClient _http;
        public AppointmentController(IHttpClientFactory factory)
        {
            _http = factory.CreateClient("WebApiClient");
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(decimal id, string status)
        {
            var res = await _http.PutAsJsonAsync($"api/Admin/Appointment/{id}/status", new { status });
            TempData[res.IsSuccessStatusCode ? "Message" : "Error"] = await res.Content.ReadAsStringAsync();
            return RedirectToAction("Index");
        }
    }
}
