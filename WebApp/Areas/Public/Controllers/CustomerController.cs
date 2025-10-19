using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class CustomerController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://10.147.20.199:5131/")
        };


        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Username và password không được để trống.");
                return View();
            }

            try
            {
                // Hardcode platform
                string platform = "WEB"; // hoặc "MOBILE" nếu mobile app

                var loginData = new
                {
                    Username = username,
                    Password = password,
                    Platform = platform
                };
                var response = await _httpClient.PostAsJsonAsync("api/Public/Customer/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CustomerLoginDto>();
                    if (result == null)
                    {
                        ModelState.AddModelError("", "API trả về dữ liệu null.");
                        return View();
                    }

                    if (result.Result == "SUCCESS")
                    {
                        TempData["Message"] = $"Login thành công! ({result.Username})";

                        HttpContext.Session.SetString("CJwtToken", result.token ?? "");
                        HttpContext.Session.SetString("CUsername", result.Username ?? "");
                        HttpContext.Session.SetString("CRole", result.roles ?? "");
                        HttpContext.Session.SetString("CPlatform", platform); // lưu platform
                        HttpContext.Session.SetString("CSessionId", result.sessionId ?? ""); // nếu API trả về sessionId

                        return RedirectToAction("Index", "Home", new { area = "Public" });
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Login thất bại: {result.Result} - {result.message}");
                        return View();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError("", $"Lỗi kết nối API: {response.ReasonPhrase} - {errorContent}");
                    return View();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi kết nối API: " + ex.Message);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = HttpContext.Session.GetString("CJwtToken");
                var username = HttpContext.Session.GetString("CUsername");
                var platform = HttpContext.Session.GetString("CPlatform");
                var sessionId = HttpContext.Session.GetString("CSessionId");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    // ✅ Thêm 3 header Oracle
                    _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
                    _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
                    _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

                    _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
                    _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
                    _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);
                    var response = await _httpClient.PostAsync("api/Public/Customer/logout", null);
                }
                HttpContext.Session.Clear();
                TempData["Message"] = "Logout thành công!";

                TempData["Message"] = "Logout thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi logout: " + ex.Message;
            }

            return RedirectToAction("Login", "Customer", new { area = "Public" });
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(CustomerRegisterDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Public/Customer/register", dto);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = "Đăng ký khách hàng thành công!";
                    return RedirectToAction("Login");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError("", $"Đăng ký thất bại: {error}");
                    return View(dto);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi kết nối API: {ex.Message}");
                return View(dto);
            }
        }




        // --- DTOs ---

        public class CustomerRegisterDto
        {
            public string FullName { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
        }

        public class CustomerLoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Result { get; set; }
            [JsonPropertyName("roles")]
            public string roles { get; set; }
            public string token { get; set; }
            public string? message { get; set; }
            public string? sessionId { get; set; }
        }
        

    }
}