using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EmployeeController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://10.147.20.199:5131/")
        };

        // --- Index: lấy danh sách nhân viên ---
        [HttpGet]
        public async Task<IActionResult> Index()
        {
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

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // ✅ Thêm 3 header Oracle
                _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
                _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
                _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

                _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);

                var response = await _httpClient.GetAsync("api/Admin/Employee");

                if (response.IsSuccessStatusCode)
                {
                    var employees = await response.Content.ReadFromJsonAsync<List<EmployeeDto>>();
                    return View(employees);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Session Oracle bị kill → redirect login
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
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

        
        // --- Login ---
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

                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result == null)
                    {
                        ModelState.AddModelError("", "API trả về dữ liệu null.");
                        return View();
                    }

                    if (result.result == "SUCCESS")
                    {
                        // Lưu session
                        HttpContext.Session.SetString("JwtToken", result.token ?? "");
                        HttpContext.Session.SetString("Username", result.Username ?? "");
                        HttpContext.Session.SetString("Role", result.roles ?? "");
                        HttpContext.Session.SetString("Platform", platform); // lưu platform
                        HttpContext.Session.SetString("SessionId", result.sessionId ?? ""); // nếu API trả về sessionId

                        TempData["Message"] = $"Login thành công! ({result.roles})";
                        return RedirectToAction("Index", "Home", new { area = "Admin" });
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Login thất bại: {result.result}");
                        return View();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError("", $"Lỗi đăng nhập: {errorContent}");
                    return View();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi kết nối API: " + ex.Message);
                return View();
            }
        }


        // --- Logout ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                var username = HttpContext.Session.GetString("Username");
                var platform = HttpContext.Session.GetString("Platform");
                var sessionId = HttpContext.Session.GetString("SessionId");
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
                    await _httpClient.PostAsync("api/Admin/Employee/logout", null);
                }

                // Chỉ xóa session liên quan đến Admin
                HttpContext.Session.Remove("JwtToken");
                HttpContext.Session.Remove("Username");
                HttpContext.Session.Remove("Role");
                HttpContext.Session.Remove("Platform");
                HttpContext.Session.Remove("SessionId");

                TempData["Message"] = "Logout thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi logout: " + ex.Message;
            }

            return RedirectToAction("Login", "Employee", new { area = "Admin" });
        }

        // --- Register ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(EmployeeRegisterDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                var username = HttpContext.Session.GetString("Username");
                var platform = HttpContext.Session.GetString("Platform");
                var sessionId = HttpContext.Session.GetString("SessionId");
                if (string.IsNullOrEmpty(token))
                    return RedirectToAction("Login");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // ✅ Thêm 3 header Oracle
                _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
                _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
                _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

                _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
                _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);

                // Gửi POST request
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/register", dto);

                if (response.IsSuccessStatusCode)
                {
                    // ✅ Đọc file stream từ API
                    var fileStream = await response.Content.ReadAsStreamAsync();
                    var contentDisposition = response.Content.Headers.ContentDisposition;
                    var fileName = contentDisposition?.FileName?.Trim('"') ?? $"{dto.Username}_private_key.txt";

                    // Trả file cho user tải về
                    return File(fileStream, "text/plain", fileName);
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


        // --- Unlock/Lock ---
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
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                    return RedirectToAction("Login");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                    return RedirectToAction("Login");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
            public string Roles { get; set; }
        }

        public class EmployeeRegisterDto
        {
            public string FullName { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
        }

        public class LoginResponse
        {
            [JsonPropertyName("username")]
            public string Username { get; set; }
            [JsonPropertyName("roles")]
            public string roles { get; set; }
            [JsonPropertyName("result")]
            public string result { get; set; }
            [JsonPropertyName("token")]
            public string token { get; set; }
            public string? message { get; set; }
            public string? sessionId { get; set; }
        }

        public class UnlockResponse
        {
            public string Username { get; set; }
            public string Result { get; set; }
        }
    }
}
