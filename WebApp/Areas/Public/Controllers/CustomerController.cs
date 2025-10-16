using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class CustomerController : Controller
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClientHandler _handler = new HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
        private static readonly HttpClient _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://localhost:7179/")
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
                var loginData = new { Username = username, Password = password };
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

                        // --- Lưu vào session WebApp ---
                        HttpContext.Session.SetString("customerUsername", result.Username ?? "");
                        HttpContext.Session.SetString("customerLoginResult", result.Result ?? "");

                        // --- Cookie WebAPI đã được _cookieContainer giữ tự động ---
                        return RedirectToAction("Index", "Home", new { area = "Public" });
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Login thất bại: {result.Result}");
                        return View();
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Lỗi kết nối API: " + response.ReasonPhrase);
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
                // 1. Gọi API logout để xóa session WebAPI
                var response = await _httpClient.PostAsync("api/Public/Customer/logout", null);
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Logout API thất bại: " + response.ReasonPhrase;
                }

                // 2. Xóa session WebApp
                HttpContext.Session.Remove("customerUsername");
                HttpContext.Session.Remove("customerLoginResult");

                // 3. Xóa cookie WebAPI trong CookieContainer
                var cookies = _cookieContainer.GetCookies(_httpClient.BaseAddress);
                foreach (Cookie cookie in cookies)
                {
                    cookie.Expired = true;
                }

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
        }

    }
}
