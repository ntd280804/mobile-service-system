using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProfileController : Controller
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        };
        private static readonly HttpClient _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://localhost:7179/") // địa chỉ API backend của bạn
        };

        public class UserDto
        {
            public string Username { get; set; }
            public string Profile { get; set; }
        }

        public class ProfileDto
        {
            public string ProfileName { get; set; }
            public string FailedLogin { get; set; }
            public string LifeTime { get; set; }
            public string GraceTime { get; set; }
            public string LockTime { get; set; }
        }

        public class AssignProfileRequest
        {
            public string Username { get; set; }
            public string ProfileName { get; set; }
        }

        public class UserProfileViewModel
        {
            public List<UserDto> Users { get; set; } = new();
            public List<ProfileDto> Profiles { get; set; } = new();
        }

        // =============================
        // Tạo Profile
        // =============================
        public class CreateProfileRequest
        {
            public string ProfileName { get; set; } = string.Empty;
            public string FailedLogin { get; set; } = "UNLIMITED";
            public string LifeTime { get; set; } = "UNLIMITED";
            public string GraceTime { get; set; } = "UNLIMITED";
            public string LockTime { get; set; } = "UNLIMITED";
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProfile(CreateProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ProfileName))
            {
                TempData["CreateProfileMessage"] = "❌ Tên profile không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var json = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(request),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync("api/Admin/Profile", json);

                if (response.IsSuccessStatusCode)
                {
                    TempData["CreateProfileMessage"] = $"✅ Đã tạo profile '{request.ProfileName}' thành công.";
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    TempData["CreateProfileMessage"] = $"❌ Lỗi tạo profile: {msg}";
                }
            }
            catch (Exception ex)
            {
                TempData["CreateProfileMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }


        // =============================
        // Xóa Profile
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                TempData["DeleteProfileMessage"] = "❌ Tên profile không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var response = await _httpClient.DeleteAsync($"api/Admin/Profile/{Uri.EscapeDataString(profileName)}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["DeleteProfileMessage"] = $"✅ Đã xóa profile '{profileName}' thành công.";
                }
                else
                {
                    TempData["DeleteProfileMessage"] = $"❌ Lỗi xóa profile: {response.ReasonPhrase}";
                }
            }
            catch (Exception ex)
            {
                TempData["DeleteProfileMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // Gán Profile cho User
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignProfile(string username, string profileName)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(profileName))
            {
                TempData["AssignProfileMessage"] = "❌ Vui lòng chọn user và profile.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var payload = new AssignProfileRequest
                {
                    Username = username,
                    ProfileName = profileName
                };

                var json = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync("api/Admin/Profile/assign", json);

                if (response.IsSuccessStatusCode)
                {
                    TempData["AssignProfileMessage"] = $"✅ Đã gán profile '{profileName}' cho user '{username}'.";
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    TempData["AssignProfileMessage"] = $"❌ Gán profile thất bại: {msg}";
                }
            }
            catch (Exception ex)
            {
                TempData["AssignProfileMessage"] = $"🚨 Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // Trang Index hiển thị user + profile
        // =============================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
                return RedirectToAction("Login", "Employee", new { area = "Admin" });

            var model = new UserProfileViewModel();

            try
            {
                // Lấy danh sách user
                var responseUser = await _httpClient.GetAsync("api/Admin/Profile/users");
                if (responseUser.IsSuccessStatusCode)
                {
                    model.Users = await responseUser.Content.ReadFromJsonAsync<List<UserDto>>() ?? new List<UserDto>();
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách user: " + responseUser.ReasonPhrase;
                }

                // Lấy danh sách profile
                var responseProfile = await _httpClient.GetAsync("api/Admin/Profile");
                if (responseProfile.IsSuccessStatusCode)
                {
                    model.Profiles = await responseProfile.Content.ReadFromJsonAsync<List<ProfileDto>>() ?? new List<ProfileDto>();
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách profile: " + responseProfile.ReasonPhrase;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return View(model);
        }
    }
}
