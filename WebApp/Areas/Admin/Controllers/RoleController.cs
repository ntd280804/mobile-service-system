using Microsoft.AspNetCore.Mvc;
using System.Net;
using static WebApp.Areas.Admin.Controllers.EmployeeController;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RoleController : Controller
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClientHandler _handler = new HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
        private static readonly HttpClient _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://localhost:7179/")
        };
        public class UserDto
        {
            public string Username { get; set; }
        }

        public class Role
        {
            public string role { get; set; }
        }

        public class UserRoleViewModel
        {
            public List<UserDto> Users { get; set; } = new();
            public List<Role> Roles { get; set; } = new();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            // 1️⃣ Kiểm tra đầu vào
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["CreateRoleMessage"] = "❌ Tên role không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // 2️⃣ Chuẩn bị dữ liệu gửi lên API
                

                // 3️⃣ Gọi API POST
                var response = await _httpClient.PostAsync($"api/Admin/Role/createrole/{Uri.EscapeDataString(roleName)}", null);


                // 4️⃣ Kiểm tra kết quả
                if (response.IsSuccessStatusCode)
                {
                    // Nếu API trả JSON bạn có thể đọc về nếu cần
                    var result = await response.Content.ReadAsStringAsync();

                    TempData["CreateRoleMessage"] = $"✅ Đã tạo role '{roleName}' thành công.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["CreateRoleMessage"] = $"❌ Lỗi tạo role: {response.ReasonPhrase}";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // 5️⃣ Xử lý lỗi
                TempData["CreateRoleMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            // 1️⃣ Kiểm tra đầu vào
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["DeleteRoleMessage"] = "❌ Tên role không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // 2️⃣ Gọi API DELETE
                var response = await _httpClient.DeleteAsync($"api/Admin/Role/deleterole/{Uri.EscapeDataString(roleName)}");

                // 3️⃣ Kiểm tra kết quả
                if (response.IsSuccessStatusCode)
                {
                    TempData["DeleteRoleMessage"] = $"✅ Đã xóa role '{roleName}' thành công.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["DeleteRoleMessage"] = $"❌ Lỗi xóa role: {response.ReasonPhrase}";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // 4️⃣ Xử lý lỗi
                TempData["DeleteRoleMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
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
                var payload = new
                {
                    UserName = userName,
                    RoleName = roleName
                };

                var json = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

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
                var payload = new
                {
                    UserName = userName,
                    RoleName = roleName
                };

                var json = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

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
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
                return RedirectToAction("Login", "Employee", new { area = "Admin" });

            var model = new UserRoleViewModel();

            try
            {
                // Lấy users
                var responseUser = await _httpClient.GetAsync("api/Admin/Role/users");
                if (responseUser.IsSuccessStatusCode)
                {
                    model.Users = await responseUser.Content.ReadFromJsonAsync<List<UserDto>>();
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách user: " + responseUser.ReasonPhrase;
                }

                //Lấy roles
                var responseRole = await _httpClient.GetAsync("api/Admin/Role/roles");
                if (responseRole.IsSuccessStatusCode)
                {
                    model.Roles = await responseRole.Content.ReadFromJsonAsync<List<Role>>() ?? new List<Role>();
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách role: " + responseRole.ReasonPhrase;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return View(model);
        }



        public class RoleDto
        {
            public string GrantedRole { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetRolesOfUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { message = "Username is required." });

            try
            {
                // Gọi API nội bộ (nên thêm slash ở đầu để tránh lỗi URL nếu cấu hình base address)
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

    }
}
