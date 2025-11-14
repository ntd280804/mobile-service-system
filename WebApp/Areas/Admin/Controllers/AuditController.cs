using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using WebApp.Helpers;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuditController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _oracleClientHelper;

        public AuditController(IHttpClientFactory httpClientFactory, OracleClientHelper oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _oracleClientHelper = oracleClientHelper;
        }
        public class AuditLogDto
        {
            public long LogId { get; set; }
            public DateTime? EventTs { get; set; }
            public string DbUser { get; set; }
            public string OsUser { get; set; }
            public string Machine { get; set; }
            public string Module { get; set; }
            public string AppRole { get; set; }
            public string EmpId { get; set; }
            public string CustomerPhone { get; set; }
            public string ObjectName { get; set; }
            public string Note { get; set; }
            public string DmlType { get; set; }
            public string ChangedColumns { get; set; }
            public string OldValues { get; set; }
            public string NewValues { get; set; }
        }

        public async Task<IActionResult> Index()
        {
            if (!_oracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                var response = await _httpClient.GetAsync("api/admin/audit");

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["Error"] = "Bạn không có quyền truy cập audit log. Chỉ ROLE_ADMIN mới được phép.";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = $"Lỗi khi lấy audit log: {response.StatusCode} - {response.ReasonPhrase}";
                    return View(new List<object>());
                }

                var content = await response.Content.ReadAsStringAsync();
                var auditLogs = JsonSerializer.Deserialize<List<AuditLogDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<AuditLogDto>();

                return View(auditLogs);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                return View(new List<object>());
            }
        }
    }
}

