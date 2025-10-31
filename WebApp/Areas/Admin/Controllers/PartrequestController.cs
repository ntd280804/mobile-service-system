using Microsoft.AspNetCore.Mvc;
using NuGet.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WebApp.Helpers;
using WebApp.Models;
namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PartrequestController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public PartrequestController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }
        // GET: /Admin/partrequest
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {

                var response = await _httpClient.GetAsync("api/admin/partrequest/getallpartrequest");
                if (!response.IsSuccessStatusCode)
                {
                    // Try to read error details from API response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách part request: {response.ReasonPhrase} - {errorContent}";
                    return View(new List<PartRequestViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<PartRequestViewModel>>() ?? new List<PartRequestViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<PartRequestViewModel>());
            }
        }
        

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync("api/admin/partrequest/getallpartrequest");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Employee", new { area = "Admin" });
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải chi tiết yêu cầu: {response.ReasonPhrase} - {errorContent}";
                    return RedirectToAction(nameof(Index));
                }

                var list = await response.Content.ReadFromJsonAsync<List<PartRequestViewModel>>() ?? new List<PartRequestViewModel>();
                var item = list.FirstOrDefault(x => x.REQUEST_ID == id);
                if (item == null)
                {
                    TempData["Error"] = "Không tìm thấy yêu cầu linh kiện.";
                    return RedirectToAction(nameof(Index));
                }

                return View(item);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Accept(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.PostAsync($"api/admin/partrequest/accept/{id}", null);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Employee", new { area = "Admin" });
                    }

                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Duyệt yêu cầu thất bại: {response.ReasonPhrase} - {error}";
                }
                else
                {
                    TempData["Success"] = "Đã duyệt yêu cầu linh kiện.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Deny(int id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.PostAsync($"api/admin/partrequest/deny/{id}", null);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                        HttpContext.Session.Clear();
                        return RedirectToAction("Login", "Employee", new { area = "Admin" });
                    }

                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Từ chối yêu cầu thất bại: {response.ReasonPhrase} - {error}";
                }
                else
                {
                    TempData["Success"] = "Đã từ chối yêu cầu linh kiện.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}