using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System;
using WebAPI.Services;

namespace WebAPI.Helpers
{
    public  class OracleSessionHelper
    {
        private readonly IConfiguration _config;

        public OracleSessionHelper(IConfiguration config)
        {
            _config = config;
        }
        
        /// Lấy connection Oracle từ header session. Nếu không hợp lệ, trả về UnauthorizedResult.
        
        public  OracleConnection GetConnectionOrUnauthorized(HttpContext httpContext,
            OracleConnectionManager connManager, out IActionResult unauthorizedResult)
        {
            unauthorizedResult = null;

            var username = httpContext.Request.Headers["X-Oracle-Username"].ToString();
            var platform = httpContext.Request.Headers["X-Oracle-Platform"].ToString();
            var sessionId = httpContext.Request.Headers["X-Oracle-SessionId"].ToString();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(sessionId))
            {
                unauthorizedResult = new UnauthorizedObjectResult(new { message = "Missing Oracle session headers" });
                return null;
            }

            var conn = connManager.GetConnectionIfExists(username, platform, sessionId);
            if (conn == null)
            {
                unauthorizedResult = new UnauthorizedObjectResult(new { message = "Oracle session expired. Please login again." });
                return null;
            }

            return conn;
        }

        
        /// Xử lý session bị kill hoặc terminate, xóa connection khỏi manager.
        
        public async Task HandleSessionKilled(HttpContext httpContext,
            OracleConnectionManager connManager, string username, string platform, string sessionId)
        {
            httpContext.Session.Clear();
            await connManager.RemoveConnection(username, platform, sessionId);
            Console.WriteLine($"[OracleSessionHelper] Session killed for {username}-{platform}-{sessionId}");
        }

        
        /// Lấy thông tin session từ header
        
        public bool TryGetSession(HttpContext httpContext, out string username, out string platform, out string sessionId)
        {
            username = httpContext.Request.Headers["X-Oracle-Username"].ToString();
            platform = httpContext.Request.Headers["X-Oracle-Platform"].ToString();
            sessionId = httpContext.Request.Headers["X-Oracle-SessionId"].ToString();

            return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(platform) && !string.IsNullOrEmpty(sessionId);
        }
    }
}
