using Microsoft.AspNetCore.Mvc;
using NuGet.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WebApp.Helpers;
using WebApp.Models.Part;
using WebApp.Models.Order;
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

                // Lấy danh sách parts từ yêu cầu
                try
                {
                    var partsResponse = await _httpClient.GetAsync($"api/admin/partrequest/by-request-id/{id}");
                    if (partsResponse.IsSuccessStatusCode)
                    {
                        var parts = await partsResponse.Content.ReadFromJsonAsync<List<WebApp.Models.Part.PartDto>>() ?? new List<WebApp.Models.Part.PartDto>();
                        ViewBag.Parts = parts;
                    }
                    else
                    {
                        ViewBag.Parts = new List<WebApp.Models.Part.PartDto>();
                    }
                }
                catch
                {
                    ViewBag.Parts = new List<WebApp.Models.Part.PartDto>();
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

        // GET: /Admin/Partrequest/Create/{id}
        [HttpGet]
        public async Task<IActionResult> Create(decimal id)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var username = HttpContext.Session.GetString("Username");
            var model = new CreatePartRequestDto
            {
                OrderId = id,
                EmpUsername = username ?? "",
                Status = "PENDING",
                RequestDate = DateTime.Now
            };

            try
            {
                // Load parts for dropdown (only IN_STOCK parts)
                var partsResponse = await _httpClient.GetAsync("api/admin/part/in-stock");
                if (partsResponse.IsSuccessStatusCode)
                {
                    var parts = await partsResponse.Content.ReadFromJsonAsync<List<WebApp.Models.Part.PartDto>>() ?? new List<WebApp.Models.Part.PartDto>();
                    ViewBag.Parts = parts.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem 
                    { 
                        Value = p.PartId.ToString(), 
                        Text = $"{p.Name} ({p.Serial})" 
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                ViewBag.Parts = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }

            return View(model);
        }

        // POST: /Admin/Partrequest/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePartRequestDto model)
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            if (model.Items == null || !model.Items.Any())
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 linh kiện";
                // Reload dropdowns
                await LoadDropdowns();
                return View(model);
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/admin/partrequest/post", model);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Tạo yêu cầu linh kiện thành công";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Tạo yêu cầu thất bại: {msg}";
                    await LoadDropdowns();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                await LoadDropdowns();
                return View(model);
            }
        }

        private async Task LoadDropdowns()
        {
            try
            {
                var partsResponse = await _httpClient.GetAsync("api/admin/part/in-stock");
                if (partsResponse.IsSuccessStatusCode)
                {
                    var parts = await partsResponse.Content.ReadFromJsonAsync<List<WebApp.Models.Part.PartDto>>() ?? new List<WebApp.Models.Part.PartDto>();
                    ViewBag.Parts = parts.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem 
                    { 
                        Value = p.PartId.ToString(), 
                        Text = $"{p.Name} ({p.Serial})" 
                    }).ToList();
                }
            }
            catch
            {
                ViewBag.Parts = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }
        }
    }
}