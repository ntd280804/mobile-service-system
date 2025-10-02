using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EmployeeController : Controller
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClientHandler _handler = new HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
        private static readonly HttpClient _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://localhost:7179/")
        };

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
                return RedirectToAction("Login", "Employee", new { area = "Admin" });

            try
            {
                var response = await _httpClient.GetAsync("api/Admin/Employee");
                if (response.IsSuccessStatusCode)
                {
                    var employees = await response.Content.ReadFromJsonAsync<List<EmployeeDto>>();
                    return View(employees);
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách nhân viên: " + response.ReasonPhrase;
                    return View(new List<EmployeeDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<EmployeeDto>());
            }
        }

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
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result == null)
                    {
                        ModelState.AddModelError("", "API trả về dữ liệu null.");
                        return View();
                    }

                    if (result.Result == "SUCCESS")
                    {
                        TempData["Message"] = $"Login thành công! ({result.Role})";

                        // --- Lưu vào session WebApp ---
                        HttpContext.Session.SetString("Username", result.Username ?? "");
                        HttpContext.Session.SetString("Role", result.Role ?? "");
                        HttpContext.Session.SetString("LoginResult", result.Result ?? "");

                        // --- Cookie WebAPI đã được _cookieContainer giữ tự động ---
                        return RedirectToAction("Index", "Home", new { area = "Admin" });
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
                var response = await _httpClient.PostAsync("api/Admin/Employee/logout", null);
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Logout API thất bại: " + response.ReasonPhrase;
                }

                // 2. Xóa session WebApp
                HttpContext.Session.Clear();

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

            return RedirectToAction("Login", "Employee", new { area = "Admin" });
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(EmployeeRegisterDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/register", dto);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = "Đăng ký nhân viên thành công!";
                    return RedirectToAction("Index");
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Username không được để trống.";
                return RedirectToAction("Index");
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/unlock", new { Username = username });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UnlockResponse>();
                    TempData["Message"] = $"Unlock tài khoản '{username}': {result.Result}";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Unlock thất bại: {error}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Username không được để trống.";
                return RedirectToAction("Index");
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/lock", new { Username = username });
                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = $"Lock tài khoản '{username}' thành công.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Lock thất bại: {error}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // --- DTOs ---
        public class EmployeeDto
        {
            public decimal EmpId { get; set; }
            public string FullName { get; set; }
            public string Status { get; set; }
            public string Username { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Role { get; set; }
        }

        public class EmployeeRegisterDto
        {
            public string FullName { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Role { get; set; }
        }

        public class LoginResponse
        {
            [JsonPropertyName("username")]
            public string Username { get; set; }
            [JsonPropertyName("role")]
            public string Role { get; set; }
            [JsonPropertyName("result")]
            public string Result { get; set; }
        }

        public class UnlockResponse
        {
            public string Username { get; set; }
            public string Result { get; set; }
        }
    }
}
