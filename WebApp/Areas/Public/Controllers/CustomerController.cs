using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Services;
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