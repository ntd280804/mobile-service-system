using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EmployeeController : Controller
    {
        private readonly HttpClient _httpClient;

        public EmployeeController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToAction("Login", "Employee", new { area = "Admin" });
            }
            try
            {
                var response = await _httpClient.GetAsync("https://localhost:7179/api/Employee");

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
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

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
                var response = await _httpClient.PostAsJsonAsync("https://localhost:7179/api/Employee/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                    if (result == null)
                    {
                        ModelState.AddModelError("", "API trả về dữ liệu null.");
                        return View();
                    }



                    // Kiểm tra kết quả login
                    if (result.Result == "SUCCESS")
                    {
                        TempData["Message"] = $"Login thành công! ({result.Role})";
                        // Lưu tất cả vào session
                        HttpContext.Session.SetString("Username", result.Username ?? "");
                        HttpContext.Session.SetString("Role", result.Role ?? "");
                        HttpContext.Session.SetString("LoginResult", result.Result ?? "");
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
        public class LoginResponse
        {
            [JsonPropertyName("username")]
            public string Username { get; set; }

            [JsonPropertyName("role")]
            public string Role { get; set; } // ADMIN / USER / null nếu login thất bại

            [JsonPropertyName("result")]
            public string Result { get; set; } // SUCCESS / FAILED / ACCOUNT LOCKED / NO EMPLOYEE
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            // Xóa tất cả session
            HttpContext.Session.Clear();

            // Redirect về trang Login
            return RedirectToAction("Login", "Employee", new { area = "Admin" });
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(EmployeeRegisterDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                var response = await _httpClient.PostAsJsonAsync("https://localhost:7179/api/Employee/register", dto);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = "Đăng ký nhân viên thành công!";
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
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


        public class EmployeeRegisterDto
        {
            public string FullName { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Role { get; set; }
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
                var response = await _httpClient.PostAsJsonAsync(
                    "https://localhost:7179/api/Employee/unlock",
                    new { Username = username }
                );

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
                var response = await _httpClient.PostAsJsonAsync(
                    "https://localhost:7179/api/Employee/lock",
                    new { Username = username }
                );

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

        public class UnlockResponse
        {
            public string Username { get; set; }
            public string Result { get; set; } // UNLOCKED / FAILED / NO EMPLOYEE
        }
    }
}

