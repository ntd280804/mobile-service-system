using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;

namespace WebApp.Helpers
{
    public class OracleClientHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OracleClientHelper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Lấy header Oracle từ session và set cho HttpClient.
        /// Nếu thiếu thông tin session, trả về IActionResult redirect về Login.
        /// </summary>
        /// <param name="httpClient">HttpClient cần set header</param>
        /// <param name="redirectToLogin">IActionResult redirect nếu session thiếu</param>
        /// <param name="isAdmin">true nếu Admin, false nếu Public</param>
        /// <returns>true nếu set header thành công, false nếu redirect</returns>
        public bool TrySetHeaders(HttpClient httpClient, out IActionResult redirectToLogin, bool isAdmin = true)
        {
            redirectToLogin = null;
            var httpContext = _httpContextAccessor.HttpContext;
            // Prefix key: "" cho Admin, "C" cho Public
            string prefix = isAdmin ? "" : "C";
            var token = httpContext.Session.GetString($"{prefix}JwtToken");
            var username = httpContext.Session.GetString($"{prefix}Username");
            var platform = httpContext.Session.GetString($"{prefix}Platform");
            var sessionId = httpContext.Session.GetString($"{prefix}SessionId");
            if (string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(platform) ||
                string.IsNullOrEmpty(sessionId))
            {
                // Redirect về Login tương ứng
                if (isAdmin)
                    redirectToLogin = new RedirectToActionResult("Login", "Employee", new { area = "Admin" });
                else
                    redirectToLogin = new RedirectToActionResult("Login", "Customer", new { area = "Public" });
                return false;
            }
            var testToken = token;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", testToken);

            httpClient.DefaultRequestHeaders.Remove("X-Oracle-Username");
            httpClient.DefaultRequestHeaders.Remove("X-Oracle-Platform");
            httpClient.DefaultRequestHeaders.Remove("X-Oracle-SessionId");

            httpClient.DefaultRequestHeaders.Add("X-Oracle-Username", username);
            httpClient.DefaultRequestHeaders.Add("X-Oracle-Platform", platform);
            httpClient.DefaultRequestHeaders.Add("X-Oracle-SessionId", sessionId);
            return true;
        }

        /// <summary>
        /// Xóa session khi logout hoặc session expired
        /// </summary>
        public void ClearSession(bool isAdmin = true)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string prefix = isAdmin ? "" : "C";

            httpContext.Session.Remove($"{prefix}JwtToken");
            httpContext.Session.Remove($"{prefix}Username");
            httpContext.Session.Remove($"{prefix}Platform");
            httpContext.Session.Remove($"{prefix}SessionId");
            httpContext.Session.Remove($"{prefix}PrivateKeyBase64");
            httpContext.Session.Remove($"{prefix}PrivateKeyBase");
        }

        /// <summary>
        /// Lấy thông tin session mà không set header
        /// </summary>
        public bool TryGetSession(out string token, out string username, out string platform, out string sessionId, bool isAdmin = true)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string prefix = isAdmin ? "" : "C";

            token = httpContext.Session.GetString($"{prefix}JwtToken");
            username = httpContext.Session.GetString($"{prefix}Username");
            platform = httpContext.Session.GetString($"{prefix}Platform");
            sessionId = httpContext.Session.GetString($"{prefix}SessionId");

            return !string.IsNullOrEmpty(token) &&
                   !string.IsNullOrEmpty(username) &&
                   !string.IsNullOrEmpty(platform) &&
                   !string.IsNullOrEmpty(sessionId);
        }
    }
}
