using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using WebApp.Helpers;
using WebApp.Models.Audit;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuditController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _oracleClientHelper;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AuditController(IHttpClientFactory httpClientFactory, OracleClientHelper oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _oracleClientHelper = oracleClientHelper;
        }

        public async Task<IActionResult> TriggerAudit()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var viewModel = new TriggerAuditViewModel();

            try
            {
                // Lấy status
                try
                {
                    var statusResponse = await _httpClient.GetAsync("api/admin/audit/status");
                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var statusContent = await statusResponse.Content.ReadAsStringAsync();
                        var auditStatus = JsonSerializer.Deserialize<AuditStatusDto>(statusContent, _jsonOptions);
                        viewModel.Status = auditStatus?.TriggerAudit;
                    }
                    else
                    {
                        var errorContent = await statusResponse.Content.ReadAsStringAsync();
                        TempData["Warning"] = $"Không thể lấy trạng thái audit: {statusResponse.StatusCode} - {errorContent}";
                    }
                }
                catch (Exception statusEx)
                {
                    TempData["Warning"] = $"Lỗi khi lấy trạng thái: {statusEx.Message}";
                }

                // Lấy audit logs
                var response = await _httpClient.GetAsync("api/admin/audit");

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền truy cập audit log. Chỉ ROLE_ADMIN mới được phép.";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = $"Lỗi khi lấy audit log: {response.StatusCode} - {response.ReasonPhrase}";
                    return View(viewModel);
                }

                var content = await response.Content.ReadAsStringAsync();
                viewModel.Logs = JsonSerializer.Deserialize<List<TriggerAuditLogDto>>(content, _jsonOptions)
                                ?? new List<TriggerAuditLogDto>();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                return View(viewModel);
            }
        }

        public async Task<IActionResult> StandardAudit()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var viewModel = new StandardAuditViewModel();

            try
            {
                // Lấy status
                try
                {
                    var statusResponse = await _httpClient.GetAsync("api/admin/audit/status");
                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var statusContent = await statusResponse.Content.ReadAsStringAsync();
                        var auditStatus = JsonSerializer.Deserialize<AuditStatusDto>(statusContent, _jsonOptions);
                        viewModel.Status = auditStatus?.StandardAudit;
                    }
                    else
                    {
                        var errorContent = await statusResponse.Content.ReadAsStringAsync();
                        TempData["Warning"] = $"Failed to get audit status: {statusResponse.StatusCode} - {errorContent}";
                    }
                }
                catch (Exception statusEx)
                {
                    TempData["Warning"] = $"Error getting audit status: {statusEx.Message}";
                }

                // Lấy audit logs
                var response = await _httpClient.GetAsync("api/admin/audit/standard");

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền truy cập audit log. Chỉ ROLE_ADMIN mới được phép.";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = $"Lỗi khi lấy standard audit: {response.StatusCode} - {response.ReasonPhrase}";
                    return View(viewModel);
                }

                var content = await response.Content.ReadAsStringAsync();
                viewModel.Logs = JsonSerializer.Deserialize<List<StandardAuditLogDto>>(content, _jsonOptions)
                                ?? new List<StandardAuditLogDto>();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                return View(viewModel);
            }
        }

        public async Task<IActionResult> FGAAudit()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var viewModel = new FgaAuditViewModel();

            try
            {
                // Lấy status
                try
                {
                    var statusResponse = await _httpClient.GetAsync("api/admin/audit/status");
                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var statusContent = await statusResponse.Content.ReadAsStringAsync();
                        var auditStatus = JsonSerializer.Deserialize<AuditStatusDto>(statusContent, _jsonOptions);
                        viewModel.Status = auditStatus?.FgaAudit;
                    }
                    else
                    {
                        var errorContent = await statusResponse.Content.ReadAsStringAsync();
                        TempData["Warning"] = $"Failed to get audit status: {statusResponse.StatusCode} - {errorContent}";
                    }
                }
                catch (Exception statusEx)
                {
                    TempData["Warning"] = $"Error getting audit status: {statusEx.Message}";
                }

                // Lấy audit logs
                var response = await _httpClient.GetAsync("api/admin/audit/fga");

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền truy cập audit log. Chỉ ROLE_ADMIN mới được phép.";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = $"Lỗi khi lấy FGA audit: {response.StatusCode} - {response.ReasonPhrase}";
                    return View(viewModel);
                }

                var content = await response.Content.ReadAsStringAsync();
                viewModel.Logs = JsonSerializer.Deserialize<List<FgaAuditLogDto>>(content, _jsonOptions)
                                ?? new List<FgaAuditLogDto>();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> EnableTriggerAudit() =>
            PostAuditCommandAsync("api/admin/audit/trigger/enable", "Đã bật trigger audit.", nameof(TriggerAudit));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DisableTriggerAudit() =>
            PostAuditCommandAsync("api/admin/audit/trigger/disable", "Đã tắt trigger audit.", nameof(TriggerAudit));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> EnableStandardAudit() =>
            PostAuditCommandAsync("api/admin/audit/standard/enable", "Đã bật standard audit.", nameof(StandardAudit));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DisableStandardAudit() =>
            PostAuditCommandAsync("api/admin/audit/standard/disable", "Đã tắt standard audit.", nameof(StandardAudit));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> EnableFgaAudit() =>
            PostAuditCommandAsync("api/admin/audit/fga/enable", "Đã bật FGA audit.", nameof(FGAAudit));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DisableFgaAudit() =>
            PostAuditCommandAsync("api/admin/audit/fga/disable", "Đã tắt FGA audit.", nameof(FGAAudit));

        private async Task<IActionResult> PostAuditCommandAsync(string endpoint, string successMessage, string redirectAction)
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, endpoint));

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền thực hiện thao tác này.";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                var content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = successMessage;
                }
                else
                {
                    var detail = string.IsNullOrWhiteSpace(content)
                        ? $"{response.StatusCode} - {response.ReasonPhrase}"
                        : content;
                    TempData["Error"] = $"Lỗi khi thực thi: {detail}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction(redirectAction);
        }
    }
}

