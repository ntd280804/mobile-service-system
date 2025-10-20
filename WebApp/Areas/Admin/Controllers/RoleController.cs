using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RoleController : Controller
    {
        private readonly HttpClient _httpClient;

        public RoleController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
        }

        // DTOs
        public class UserDto
        {
            public string Username { get; set; }
        }

        public class RoleDto
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }
            [JsonPropertyName("grantedRole")]
            public string GrantedRole { get; set; } // for roles-of-user
        }

        public class UserRoleViewModel
        {
            public List<UserDto> Users { get; set; } = new();
            public List<RoleDto> Roles { get; set; } = new();
        }

        // GET: /Admin/Role
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            var username = HttpContext.Session.GetString("Username");
            var platform = HttpContext.Session.GetString("Platform");
            var sessionId = HttpContext.Session.GetString("SessionId");

            if (string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
            {
                return RedirectToAction("Login", "Employee", new { area = "Admin" });
            }

            var model = new UserRoleViewModel();

            try
            {
                SetOracleHeadersFromSession();

                // Get users
                var responseUser = await _httpClient.GetAsync("api/Admin/Role/users");
                if (responseUser.IsSuccessStatusCode)
                {
                    model.Users = await responseUser.Content.ReadFromJsonAsync<List<UserDto>>() ?? new List<UserDto>();
                }
                else
                {
                    var errorContent = await responseUser.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách user: {responseUser.ReasonPhrase} - {errorContent}";
                }

                // Get roles
                var responseRole = await _httpClient.GetAsync("api/Admin/Role/roles");
                if (responseRole.IsSuccessStatusCode)
                {
                    model.Roles = await responseRole.Content.ReadFromJsonAsync<List<RoleDto>>() ?? new List<RoleDto>();
                }
                else
                {
                    var errorContent = await responseRole.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách role: {responseRole.ReasonPhrase} - {errorContent}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["CreateRoleMessage"] = "❌ Tên role không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SetOracleHeadersFromSession();

                var response = await _httpClient.PostAsync($"api/Admin/Role/createrole/{Uri.EscapeDataString(roleName)}", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["CreateRoleMessage"] = $"✅ Đã tạo role '{roleName}' thành công.";
                }
                else
                {
                    TempData["CreateRoleMessage"] = $"❌ Lỗi tạo role: {response.ReasonPhrase}";
                }
            }
            catch (Exception ex)
            {
                TempData["CreateRoleMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["DeleteRoleMessage"] = "❌ Tên role không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SetOracleHeadersFromSession();

                var response = await _httpClient.DeleteAsync($"api/Admin/Role/deleterole/{Uri.EscapeDataString(roleName)}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["DeleteRoleMessage"] = $"✅ Đã xóa role '{roleName}' thành công.";
                }
                else
                {
                    TempData["DeleteRoleMessage"] = $"❌ Lỗi xóa role: {response.ReasonPhrase}";
                }
            }
            catch (Exception ex)
            {
                TempData["DeleteRoleMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userName, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["AssignRoleMessage"] = "❌ Vui lòng chọn user và role.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SetOracleHeadersFromSession();

                var payload = new { UserName = userName, RoleName = roleName };
                var json = JsonContent.Create(payload);

                var response = await _httpClient.PostAsync("api/Admin/Role/assignrole", json);

                if (response.IsSuccessStatusCode)
                {
                    TempData["AssignRoleMessage"] = $"✅ Đã gán role '{roleName}' cho user '{userName}'.";
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    TempData["AssignRoleMessage"] = $"❌ Gán role thất bại: {msg}";
                }
            }
            catch (Exception ex)
            {
                TempData["AssignRoleMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeRole(string userName, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["RevokeRoleMessage"] = "❌ Vui lòng chọn user và role.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                SetOracleHeadersFromSession();

                var payload = new { UserName = userName, RoleName = roleName };
                var json = JsonContent.Create(payload);

                var response = await _httpClient.PostAsync("api/Admin/Role/revokerole", json);

                if (response.IsSuccessStatusCode)
                {
                    TempData["RevokeRoleMessage"] = $"✅ Đã thu hồi role '{roleName}' khỏi user '{userName}'.";
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    TempData["RevokeRoleMessage"] = $"❌ Thu hồi thất bại: {msg}";
                }
            }
            catch (Exception ex)
            {
                TempData["RevokeRoleMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetRolesOfUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { message = "Username is required." });

            try
            {
                SetOracleHeadersFromSession();

                var result = await _httpClient.GetFromJsonAsync<List<RoleDto>>(
                    $"api/Admin/Role/roles-of-user/{Uri.EscapeDataString(username)}"
                );

                if (result == null)
                    return NotFound(new { message = "Không tìm thấy role cho user này." });

                return Ok(result);
            }
            catch (HttpRequestException httpEx)
            {
                return StatusCode(502, new { message = "Không thể kết nối API role.", detail = httpEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống.", detail = ex.Message });
            }
        }

        // Helper to set Oracle headers
        private void SetOracleHeaders(string token, string username, string platform, string sessionId)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
            _httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

            _httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
            _httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);
        }

        private void SetOracleHeadersFromSession()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            var username = HttpContext.Session.GetString("Username");
            var platform = HttpContext.Session.GetString("Platform");
            var sessionId = HttpContext.Session.GetString("SessionId");
            SetOracleHeaders(token, username, platform, sessionId);
        }
    }
}