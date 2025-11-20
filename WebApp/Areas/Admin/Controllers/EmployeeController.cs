using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Models.Auth;
using WebApp.Models;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EmployeeController : Controller
        
    {
        private readonly HttpClient _httpClient;
        private readonly SecurityClient _securityClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public EmployeeController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper, SecurityClient securityClient)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
            _securityClient = securityClient;
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Vui lòng đăng nhập trước.";
                return RedirectToAction("Login");
            }

            return View(new CustomerChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(CustomerChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var keyResp = await _httpClient.GetFromJsonAsync<WebApiResponse<string>>("api/public/security/server-public-key");
                if (keyResp == null || !keyResp.Success || string.IsNullOrWhiteSpace(keyResp.Data))
                {
                    ModelState.AddModelError(string.Empty, "Không thể lấy khóa công khai của máy chủ.");
                    return View(model);
                }

                var payload = new
                {
                    OldPassword = model.OldPassword,
                    NewPassword = model.NewPassword
                };

                var json = JsonSerializer.Serialize(payload);
                var encrypted = EncryptHelper.HybridEncrypt(json, keyResp.Data);

                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/change-password-secure", new
                {
                    encryptedKeyBlockBase64 = encrypted.EncryptedKeyBlock,
                    cipherDataBase64 = encrypted.CipherData
                });

                var apiResult = await response.Content.ReadFromJsonAsync<WebApiResponse<string>>();
                if (response.IsSuccessStatusCode && apiResult?.Success == true)
                {
                    TempData["Message"] = "Đổi mật khẩu thành công.";
                    return RedirectToAction("Index", "Home", new { area = "Public" });
                }

                var errorMsg = apiResult?.Error ?? await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Đổi mật khẩu thất bại: {errorMsg}");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Lỗi kết nối API: " + ex.Message);
                return View(model);
            }
        }
        // --- Index: lấy danh sách nhân viên ---
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {

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

        private bool IsEmployeeLoggedIn()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            return !string.IsNullOrEmpty(token);
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
                // Hardcode platform
                string platform = "WEB"; // hoặc "MOBILE" nếu mobile app

                var loginData = new
                {
                    Username = username,
                    Password = password,
                    Platform = platform
                };

                await _securityClient.InitializeAsync(HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!, "admin-web");
                var apiResult = await _securityClient.PostEncryptedAsync<object, LoginResultEnvelope>("api/Admin/Employee/login-secure", loginData);

                if (apiResult.Result == "SUCCESS")
                {
                    HttpContext.Session.SetString("JwtToken", apiResult.Token ?? "");
                    HttpContext.Session.SetString("Username", apiResult.Username ?? "");
                    HttpContext.Session.SetString("Role", apiResult.Roles ?? "");
                    HttpContext.Session.SetString("Platform", platform);
                    HttpContext.Session.SetString("SessionId", apiResult.SessionId ?? "");

                    TempData["Message"] = $"Login thành công! ({apiResult.Roles})";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }
                ModelState.AddModelError("", $"Login thất bại: {apiResult.Result} - {apiResult.Message}");
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi kết nối API: " + ex.Message);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteQrLogin([FromBody] QrLoginCompleteDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest(new { message = "Invalid QR login payload." });
            }

            HttpContext.Session.SetString("JwtToken", dto.Token);
            HttpContext.Session.SetString("Username", dto.Username);
            HttpContext.Session.SetString("Role", dto.Roles ?? string.Empty);
            HttpContext.Session.SetString("Platform", "WEB");
            HttpContext.Session.SetString("SessionId", dto.SessionId ?? Guid.NewGuid().ToString());

            TempData["Message"] = "Đăng nhập Admin bằng QR thành công.";

            var redirectUrl = Url.Action("Index", "Home", new { area = "Admin" });
            return Ok(new { redirect = redirectUrl });
        }

        [HttpGet]
        public IActionResult MobileQrLogin()
        {
            if (!IsEmployeeLoggedIn())
            {
                TempData["Error"] = "Vui lòng đăng nhập Admin Web trước.";
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMobileQrSession()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var response = await _httpClient.PostAsync("api/Public/WebToMobileQr/create", null);
            var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<WebToMobileQrCreateResponse>>();
            if (payload == null)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            return Json(payload);
        }

        [HttpGet]
        public async Task<IActionResult> MobileQrStatus(string qrLoginId)
        {
            if (string.IsNullOrWhiteSpace(qrLoginId))
                return BadRequest(new { message = "qrLoginId is required." });

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var response = await _httpClient.GetAsync($"api/Public/WebToMobileQr/status/{qrLoginId}");
            var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<WebToMobileQrStatusResponse>>();
            if (payload == null)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            return Json(payload);
        }


        // --- Logout ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
               await _httpClient.PostAsync("api/Admin/Employee/logout", null);

                // Chỉ xóa session liên quan đến Admin
                _OracleClientHelper.ClearSession();

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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/unlock", new { Username = username });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<EmployeeUnlockResponse>();
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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
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
        
    }
}
