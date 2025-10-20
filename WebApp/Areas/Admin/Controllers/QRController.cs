using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class QRController : Controller
    {
        private readonly HttpClient _httpClient;

        public QRController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
        }

        public class QRViewModel
        {
            public string Serial { get; set; }
        }

        // GET: /Admin/QR/GetQRCode?serial=RAM123456
        public async Task<IActionResult> GetQRCode(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return BadRequest("Serial is required");

            var token = HttpContext.Session.GetString("JwtToken");
            var username = HttpContext.Session.GetString("Username");
            var platform = HttpContext.Session.GetString("Platform");
            var sessionId = HttpContext.Session.GetString("SessionId");

            if (string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
            {
                return RedirectToAction("Login", "Employee", new { area = "Admin" });
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

            _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);

            // Gọi API backend
            var response = await _httpClient.GetAsync($"api/admin/qr/{serial}");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error fetching QR code from API");

            var qrBytes = await response.Content.ReadAsByteArrayAsync();
            return File(qrBytes, "image/png");
        }
    }
}
