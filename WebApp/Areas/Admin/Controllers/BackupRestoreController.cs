using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using WebApp.Helpers;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class BackupRestoreController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _oracleClientHelper;
        private readonly IConfiguration _configuration;

        public BackupRestoreController(
            IHttpClientFactory httpClientFactory,
            OracleClientHelper oracleClientHelper,
            IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _oracleClientHelper = oracleClientHelper;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToAction("Login", "Employee", new { area = "Admin" });
            }

            // Kiểm tra quyền ADMIN
            var role = HttpContext.Session.GetString("Role");
            if (string.IsNullOrEmpty(role) || !role.Contains("ROLE_ADMIN"))
            {
                TempData["Error"] = "Chỉ ADMIN mới được truy cập chức năng Backup/Restore";
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Backup()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.PostAsync("api/admin/backuprestore/backup", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền chạy backup. Chỉ ADMIN mới được phép.";
                    return RedirectToAction("Index");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response.IsSuccessStatusCode && apiResponse?.Success == true)
                {
                    TempData["Success"] = apiResponse.Data ?? "Backup job đã được khởi chạy thành công";
                }
                else
                {
                    TempData["Error"] = apiResponse?.Error ?? $"Lỗi khi chạy backup: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.PostAsync("api/admin/backuprestore/restore", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền chạy restore. Chỉ ADMIN mới được phép.";
                    return RedirectToAction("Index");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response.IsSuccessStatusCode && apiResponse?.Success == true)
                {
                    TempData["Success"] = apiResponse.Data ?? "Restore job đã được khởi chạy thành công";
                }
                else
                {
                    TempData["Error"] = apiResponse?.Error ?? $"Lỗi khi chạy restore: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Helper class for deserialization
        private class ApiResponse<T>
        {
            public bool Success { get; set; }
            public T? Data { get; set; }
            public string? Error { get; set; }
        }
    }
}

