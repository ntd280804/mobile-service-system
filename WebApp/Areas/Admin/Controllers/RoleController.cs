using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WebApp.Helpers;
using WebApp.Models.Permission;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RoleController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public RoleController(IHttpClientFactory httpClientFactory,OracleClientHelper _oracle)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracle;
        }

        // DTOs moved to WebApp.Models

        // GET: /Admin/Role
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var model = new UserRoleViewModel();

            try
            {

                // Get users
                var responseUser = await _httpClient.GetAsync("api/Admin/Role/users");
                if (responseUser.IsSuccessStatusCode)
                {
                    model.Users = await responseUser.Content.ReadFromJsonAsync<List<RoleUserDto>>() ?? new List<RoleUserDto>();
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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {


                var response = await _httpClient.PostAsync($"api/Admin/Role/{Uri.EscapeDataString(roleName)}/create", null);

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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {


                var response = await _httpClient.DeleteAsync($"api/Admin/Role/{Uri.EscapeDataString(roleName)}");

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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                

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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                

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
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                

                var result = await _httpClient.GetFromJsonAsync<List<RoleDto>>(
                    $"api/Admin/Role/{Uri.EscapeDataString(username)}/roles"
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