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
                
                string client_id = "public-web";
                
                // Kiểm tra xem có private key trong session không
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("CPrivateKeyBase64");
                if (!string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    // Decode từ Base64 về PEM
                    existingPrivateKeyPem = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));
                }
                
                // Login: sử dụng private key từ session nếu có, nếu không thì generate mới
                await _securityClient.InitializeAsync(
                    HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!, 
                    client_id, 
                    existingPrivateKeyPem);
                
                // Lưu private key vào session nếu chưa có (để dùng cho các request sau)
                if (string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    var newPrivateKeyPem = _securityClient.GetPrivateKeyPem();
                    if (!string.IsNullOrWhiteSpace(newPrivateKeyPem))
                    {
                        // Lưu private key PEM vào session (encode Base64)
                        HttpContext.Session.SetString("CPrivateKeyBase64", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(newPrivateKeyPem)));
                    }
                }
                
                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                var apiResult = await _securityClient.PostEncryptedAndGetEncryptedAsync<object, LoginResultEnvelope>(
                    "api/Public/Customer/login/secure", loginData);

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
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, string actionType)
        {
            ViewBag.OtpStatus = null;

            if (string.Equals(actionType, "SendOtp", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    ModelState.AddModelError(nameof(model.Email), "Vui lòng nhập email nhận OTP.");

                if (!ModelState.IsValid)
                    return View(model);

                try
                {
                    var request = new ForgotPasswordGenerateOtpDto
                    {
                        
                        Email = model.Email.Trim()
                    };

                    var response = await _httpClient.PostAsJsonAsync("api/Public/Customer/forgot-password/generate-otp", request);
                    var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<ForgotPasswordOtpResponse>>();

                    if (payload == null)
                    {
                        ModelState.AddModelError(string.Empty, "Không thể gửi OTP. Vui lòng thử lại sau.");
                        return View(model);
                    }

                    if (payload.Success)
                    {
                        model.OtpSent = true;
                        model.OtpStatusMessage = payload.Data?.Message ?? "Đã gửi OTP. Vui lòng kiểm tra email.";
                        ViewBag.OtpStatus = model.OtpStatusMessage;
                        ModelState.Clear();
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, payload.Error ?? "Không thể gửi OTP.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Lỗi kết nối API: " + ex.Message);
                }

                return View(model);
            }

            if (string.Equals(actionType, "ResetPassword", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    ModelState.AddModelError(nameof(model.Email), "Vui lòng nhập email nhận OTP.");
                if (string.IsNullOrWhiteSpace(model.Otp))
                    ModelState.AddModelError(nameof(model.Otp), "Vui lòng nhập mã OTP.");
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                    ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới.");
                if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
                    ModelState.AddModelError(nameof(model.ConfirmPassword), "Vui lòng xác nhận mật khẩu mới.");
                if (!string.Equals(model.NewPassword, model.ConfirmPassword))
                    ModelState.AddModelError(nameof(model.ConfirmPassword), "Mật khẩu xác nhận không khớp.");

                if (!ModelState.IsValid)
                {
                    model.OtpSent = true;
                    ViewBag.OtpStatus = model.OtpStatusMessage;
                    return View(model);
                }

                try
                {
                    var request = new ForgotPasswordResetDto
                    {
                        Email = model.Email?.Trim() ?? string.Empty,
                        Otp = model.Otp.Trim(),
                        NewPassword = model.NewPassword
                    };

                    var response = await _httpClient.PostAsJsonAsync("api/Public/Customer/forgot-password/reset", request);
                    var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<string>>();

                    if (payload == null)
                    {
                        ModelState.AddModelError(string.Empty, "Không thể đặt lại mật khẩu. Vui lòng thử lại sau.");
                        model.OtpSent = true;
                        return View(model);
                    }

                    if (payload.Success)
                    {
                        TempData["Message"] = payload.Data ?? "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại.";
                        return RedirectToAction("Login");
                    }

                    ModelState.AddModelError(string.Empty, payload.Error ?? "Không thể đặt lại mật khẩu.");
                    model.OtpSent = true;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Lỗi kết nối API: " + ex.Message);
                    model.OtpSent = true;
                }

                ViewBag.OtpStatus = model.OtpStatusMessage;
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Hành động không hợp lệ.");
            return View(model);
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
                // Lấy thông tin từ session
                var username = HttpContext.Session.GetString("CUsername");
                var platform = HttpContext.Session.GetString("CPlatform") ?? "WEB";
                if (string.IsNullOrWhiteSpace(username))
                {
                    ModelState.AddModelError(string.Empty, "Vui lòng đăng nhập trước.");
                    return View(model);
                }

                string client_id = "public-web";

                // Lấy private key từ session
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("CPrivateKeyBase64");
                if (!string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    existingPrivateKeyPem = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));
                }

                // Initialize SecurityClient với private key từ session
                await _securityClient.InitializeAsync(
                    HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!,
                    client_id,
                    existingPrivateKeyPem);

                // Set headers cho authenticated request
                var token = HttpContext.Session.GetString("CJwtToken");
                var sessionId = HttpContext.Session.GetString("CSessionId");
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sessionId))
                {
                    ModelState.AddModelError(string.Empty, "Vui lòng đăng nhập trước.");
                    return View(model);
                }
                _securityClient.SetHeaders(token, username, platform, sessionId);

                var payload = new
                {
                    OldPassword = model.OldPassword,
                    NewPassword = model.NewPassword
                };

                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                // PostEncryptedAndGetEncryptedAsync sẽ tự động giải mã và trả về Data từ ApiResponse
                await _securityClient.PostEncryptedAndGetEncryptedAsync<object, string>(
                    "api/Public/Customer/change-password/secure", payload);

                TempData["Message"] = "Đổi mật khẩu thành công.";
                return RedirectToAction("Index", "Home", new { area = "Public" });
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