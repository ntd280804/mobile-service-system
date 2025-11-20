using Microsoft.AspNetCore.Mvc;
using System;
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

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class CustomerController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly SecurityClient _securityClient;
        private readonly OracleClientHelper _OracleClientHelper;
        public CustomerController(IHttpClientFactory httpClientFactory,OracleClientHelper _or, SecurityClient securityClient)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _or;
            _securityClient = securityClient;
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
                // Hardcode platform
                string platform = "WEB"; // hoặc "MOBILE" nếu mobile app

                var loginData = new
                {
                    Username = username,
                    Password = password,
                    Platform = platform
                };
                await _securityClient.InitializeAsync(HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!, "public-web");
                var apiResult = await _securityClient.PostEncryptedAsync<object, LoginResultEnvelope>("api/Public/Customer/login-secure", loginData);

                if (apiResult.Result == "SUCCESS")
                {
                    TempData["Message"] = $"Login thành công! ({apiResult.Username})";

                    HttpContext.Session.SetString("CJwtToken", apiResult.Token ?? "");
                    HttpContext.Session.SetString("CUsername", apiResult.Username ?? "");
                    HttpContext.Session.SetString("CRole", apiResult.Roles ?? "");
                    HttpContext.Session.SetString("CPlatform", platform);
                    HttpContext.Session.SetString("CSessionId", apiResult.SessionId ?? "");

                    return RedirectToAction("Index", "Home", new { area = "Public" });
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

            HttpContext.Session.SetString("CJwtToken", dto.Token);
            HttpContext.Session.SetString("CUsername", dto.Username);
            HttpContext.Session.SetString("CRole", dto.Roles ?? string.Empty);
            HttpContext.Session.SetString("CPlatform", "WEB");
            HttpContext.Session.SetString("CSessionId", dto.SessionId ?? Guid.NewGuid().ToString());

            TempData["Message"] = "Đăng nhập bằng QR thành công.";

            var redirectUrl = Url.Action("Index", "Home", new { area = "Public" });
            return Ok(new { redirect = redirectUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> Logout()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect,false))
                return redirect;
            try
            {
                var response = await _httpClient.PostAsync("api/Public/Customer/logout", null);
                _OracleClientHelper.ClearSession(false); // Xóa session Public

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

        private bool IsCustomerLoggedIn()
        {
            var token = HttpContext.Session.GetString("CJwtToken");
            return !string.IsNullOrEmpty(token);
        }

        [HttpGet]
        public IActionResult MobileQrLogin()
        {
            if (!IsCustomerLoggedIn())
            {
                TempData["Error"] = "Vui lòng đăng nhập Web trước.";
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMobileQrSession()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect, false))
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

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect, false))
                return redirect;

            var response = await _httpClient.GetAsync($"api/Public/WebToMobileQr/status/{qrLoginId}");
            var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<WebToMobileQrStatusResponse>>();
            if (payload == null)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            return Json(payload);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            var token = HttpContext.Session.GetString("CJwtToken");
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

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect, false))
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

                var response = await _httpClient.PostAsJsonAsync("api/Public/Customer/change-password-secure", new
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

        
        

    }
}