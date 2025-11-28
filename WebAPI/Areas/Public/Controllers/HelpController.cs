using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Models;
using WebAPI.Models.Public;
using WebAPI.Services;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class HelpController : ControllerBase
    {
        private readonly EmailService _emailService;

        public HelpController(EmailService emailService)
        {
            _emailService = emailService;
        }

        [AllowAnonymous]
        [HttpPost("send-help-email")]
        public async Task<IActionResult> SendHelpEmail([FromBody] SendHelpEmailRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập đầy đủ email và nội dung."));
            }

            var (success, message) = await _emailService.SendHelpEmailAsync(request.Email, request.Content);
            if (!success)
                {
                return StatusCode(500, ApiResponse<string>.Fail(message));
                }

            return Ok(ApiResponse<string>.Ok(message));
        }
    }
}

