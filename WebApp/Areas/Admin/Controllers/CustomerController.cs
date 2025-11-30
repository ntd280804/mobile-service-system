using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models.Auth;
using WebApp.Models.Customer;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CustomerController : Controller

    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public CustomerController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }
        
        // --- Index: lấy danh sách khách hàng ---
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string phone = null, string fullName = null, string status = null)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            
            const int pageSize = 10;
            
            try
            {
                var response = await _httpClient.GetAsync("api/Admin/Customer");

                if (response.IsSuccessStatusCode)
                {
                    var customers = await response.Content.ReadFromJsonAsync<List<CustomerDto>>();
                    
                    // Client-side filtering
                    var filtered = customers ?? new List<CustomerDto>();
                    
                    if (!string.IsNullOrWhiteSpace(phone))
                        filtered = filtered.Where(c => c.Phone != null && c.Phone.Contains(phone)).ToList();
                    
                    if (!string.IsNullOrWhiteSpace(fullName))
                        filtered = filtered.Where(c => c.FullName != null && c.FullName.Contains(fullName, StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    if (!string.IsNullOrWhiteSpace(status))
                        filtered = filtered.Where(c => c.Status != null && c.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    var paginatedList = WebApp.Models.Common.PaginatedList<CustomerDto>.Create(
                        filtered, 
                        page, 
                        pageSize);
                    return View(paginatedList);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách khách hàng: " + response.ReasonPhrase;
                    var emptyList = WebApp.Models.Common.PaginatedList<CustomerDto>.Create(
                        new List<CustomerDto>(), 
                        page, 
                        pageSize);
                    return View(emptyList);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                var emptyList = WebApp.Models.Common.PaginatedList<CustomerDto>.Create(
                    new List<CustomerDto>(), 
                    page, 
                    pageSize);
                return View(emptyList);
            }
        }
        // --- Unlock/Lock ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string Phone)
        {
            if (string.IsNullOrEmpty(Phone))
            {
                TempData["Error"] = "Username không được để trống.";
                return RedirectToAction("Index");
            }
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Customer/unlock", new { Phone = Phone });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CustomerUnlockResponse>();
                    TempData["Message"] = $"Unlock tài khoản '{Phone}': {result.Result}";
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
        public async Task<IActionResult> Lock(string Phone)
        {
            if (string.IsNullOrEmpty(Phone))
            {
                TempData["Error"] = "Username không được để trống.";
                return RedirectToAction("Index");
            }
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Customer/lock", new { Phone = Phone });
                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = $"Lock tài khoản '{Phone}' thành công.";
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
        
    }
}