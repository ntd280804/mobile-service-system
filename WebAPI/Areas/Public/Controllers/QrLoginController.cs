using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using WebAPI.Models;
using WebAPI.Models.Auth;
using WebAPI.Services;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class QrLoginController : ControllerBase
    {
        private readonly QrLoginStore _qrLoginStore;
        private readonly ProxyLoginService _proxyLoginService;

        public QrLoginController(
            QrLoginStore qrLoginStore,
            ProxyLoginService proxyLoginService)
        {
            _qrLoginStore = qrLoginStore;
            _proxyLoginService = proxyLoginService;
        }

        
        /// Web gọi để tạo session QR login (TTL 2 phút).
        
        [HttpPost("create")]
        [AllowAnonymous]
        public ActionResult<ApiResponse<QrLoginCreateResponse>> Create()
        {
            var session = _qrLoginStore.CreateSession();

            var response = new QrLoginCreateResponse
            {
                QrLoginId = session.Id,
                Code = session.Code,
                ExpiresAtUtc = session.ExpiresAtUtc
            };

            return Ok(ApiResponse<QrLoginCreateResponse>.Ok(response));
        }

        
        /// Mobile (đã đăng nhập, gửi JWT) gọi để confirm đăng nhập Web bằng code.
        
        [HttpPost("confirm")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public ActionResult<ApiResponse<string>> Confirm([FromBody] QrLoginConfirmRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<string>.Fail("Code is required."));
            }
            var session = _qrLoginStore.GetByCode(request.Code);
            if (session == null || session.Status != Services.QrLoginStatus.Pending)
            {
                return BadRequest(ApiResponse<string>.Fail("Code không hợp lệ hoặc đã hết hạn."));
            }
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {return Unauthorized(ApiResponse<string>.Fail("Không xác định được username từ JWT."));}
            try
            {
                var isCustomer = HasCustomerRole(User.Claims);
                var proxyLogin = isCustomer
                    ? _proxyLoginService.LoginCustomer(username, "WEB")
                    : _proxyLoginService.LoginEmployee(username, "WEB");

                session.Status = Services.QrLoginStatus.Confirmed;
                session.Username = proxyLogin.Username;
                session.Roles = proxyLogin.Roles;
                session.WebToken = proxyLogin.Token;
                session.WebSessionId = proxyLogin.SessionId;

                return Ok(ApiResponse<string>.Ok("CONFIRMED"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail($"QR login failed: {ex.Message}"));
            }
        }

        
        /// Web poll trạng thái QR login; nếu đã confirm thì nhận Web token để login.
        
        [HttpGet("status/{qrLoginId}")]
        [AllowAnonymous]
        public ActionResult<ApiResponse<QrLoginStatusResponse>> Status(string qrLoginId)
        {
            var session = _qrLoginStore.GetById(qrLoginId);
            if (session == null)
            {
                return NotFound(ApiResponse<QrLoginStatusResponse>.Fail("Không tìm thấy phiên QR login."));
            }

            var response = new QrLoginStatusResponse
            {
                QrLoginId = session.Id,
                Code = session.Code,
                Status = session.Status == Services.QrLoginStatus.Pending
                    ? Models.Auth.QrLoginStatus.Pending
                    : session.Status == Services.QrLoginStatus.Confirmed
                        ? Models.Auth.QrLoginStatus.Confirmed
                        : Models.Auth.QrLoginStatus.Expired,
                ExpiresAtUtc = session.ExpiresAtUtc,
                Username = session.Username,
                Roles = session.Roles,
                WebToken = session.WebToken,
                WebSessionId = session.WebSessionId
            };

            return Ok(ApiResponse<QrLoginStatusResponse>.Ok(response));
        }
        private static bool HasCustomerRole(IEnumerable<Claim> claims)
        {
            return claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Any(role => role.Equals("ROLE_KHACHHANG", StringComparison.OrdinalIgnoreCase));
        }
    }
}


