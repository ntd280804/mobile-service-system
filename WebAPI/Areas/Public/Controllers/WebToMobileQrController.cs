using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
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
    public class WebToMobileQrController : ControllerBase
    {
        private readonly WebToMobileQrStore _store;
        private readonly CustomerQrLoginService _customerQrLoginService;
        private readonly EmployeeQrLoginService _employeeQrLoginService;

        public WebToMobileQrController(
            WebToMobileQrStore store,
            CustomerQrLoginService customerQrLoginService,
            EmployeeQrLoginService employeeQrLoginService)
        {
            _store = store;
            _customerQrLoginService = customerQrLoginService;
            _employeeQrLoginService = employeeQrLoginService;
        }

        [HttpPost("create")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public ActionResult<ApiResponse<WebToMobileQrCreateResponse>> Create()
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(ApiResponse<WebToMobileQrCreateResponse>.Fail("Không xác định được username."));
            }

            var roles = string.Join(",",
                User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value));

            var sourcePlatform = Request.Headers["X-Oracle-Platform"].ToString();

            var session = _store.Create(username, roles, sourcePlatform);

            var response = new WebToMobileQrCreateResponse
            {
                QrLoginId = session.Id,
                Code = session.Code,
                ExpiresAtUtc = session.ExpiresAtUtc
            };

            return Ok(ApiResponse<WebToMobileQrCreateResponse>.Ok(response));
        }

        [HttpGet("status/{qrLoginId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public ActionResult<ApiResponse<WebToMobileQrStatusResponse>> Status(string qrLoginId)
        {
            var session = _store.GetById(qrLoginId);
            if (session == null)
            {
                return NotFound(ApiResponse<WebToMobileQrStatusResponse>.Fail("Không tìm thấy phiên QR."));
            }

            var response = new WebToMobileQrStatusResponse
            {
                QrLoginId = session.Id,
                Code = session.Code,
                Status = session.Status,
                ExpiresAtUtc = session.ExpiresAtUtc
            };

            return Ok(ApiResponse<WebToMobileQrStatusResponse>.Ok(response));
        }

        [HttpPost("confirm")]
        [AllowAnonymous]
        public ActionResult<ApiResponse<WebToMobileQrConfirmResponse>> Confirm([FromBody] WebToMobileQrConfirmRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<WebToMobileQrConfirmResponse>.Fail("Code is required."));
            }

            var session = _store.GetByCode(request.Code);
            if (session == null || session.Status != WebToMobileQrStatus.Pending)
            {
                return BadRequest(ApiResponse<WebToMobileQrConfirmResponse>.Fail("Code không hợp lệ hoặc đã hết hạn."));
            }

            try
            {
                var platform = string.IsNullOrWhiteSpace(request.Platform)
                    ? "MOBILE"
                    : request.Platform!;

                WebToMobileQrConfirmResponse response;

                if (session.SourceRoles?.Contains("ROLE_KHACHHANG", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var loginResult = _customerQrLoginService.LoginViaProxy(session.SourceUsername, platform);
                    _store.MarkConfirmed(session, loginResult.Token!, loginResult.SessionId!, loginResult.Roles ?? string.Empty);
                    response = new WebToMobileQrConfirmResponse
                    {
                        Username = loginResult.Username ?? session.SourceUsername,
                        Roles = loginResult.Roles ?? string.Empty,
                        Token = loginResult.Token ?? string.Empty,
                        SessionId = loginResult.SessionId ?? string.Empty
                    };
                }
                else
                {
                    var loginResult = _employeeQrLoginService.LoginViaProxy(session.SourceUsername, platform);
                    _store.MarkConfirmed(session, loginResult.Token!, loginResult.SessionId!, loginResult.Roles ?? string.Empty);
                    response = new WebToMobileQrConfirmResponse
                    {
                        Username = loginResult.Username ?? session.SourceUsername,
                        Roles = loginResult.Roles ?? string.Empty,
                        Token = loginResult.Token ?? string.Empty,
                        SessionId = loginResult.SessionId ?? string.Empty
                    };
                }

                return Ok(ApiResponse<WebToMobileQrConfirmResponse>.Ok(response));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<WebToMobileQrConfirmResponse>.Fail(ex.Message));
            }
        }
    }
}

