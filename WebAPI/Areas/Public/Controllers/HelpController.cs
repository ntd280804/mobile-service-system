using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class HelpController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HelpController> _logger;

        public HelpController(EmailService emailService, IConfiguration configuration, ILogger<HelpController> logger)
        {
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("send-help-email")]
        public async Task<IActionResult> SendHelpEmail([FromBody] SendHelpEmailRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập đầy đủ email và nội dung."));
            }

            try
            {
                var recipientEmail = _configuration["Email:RecipientEmail"] ?? _configuration["Email:SenderEmail"];
                
                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    return StatusCode(500, ApiResponse<string>.Fail("Cấu hình email chưa được thiết lập."));
                }

                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var senderEmail = _configuration["Email:SenderEmail"];
                var senderPassword = _configuration["Email:SenderPassword"];

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
                {
                    return StatusCode(500, ApiResponse<string>.Fail("Cấu hình email chưa được thiết lập."));
                }

                using (var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword);

                    var mailMessage = new System.Net.Mail.MailMessage
                    {
                        From = new System.Net.Mail.MailAddress(senderEmail, "Mobile Service System"),
                        Subject = $"Yêu cầu trợ giúp từ: {request.Email}",
                        Body = $"Email người gửi: {request.Email}\n\nNội dung:\n{request.Content}",
                        IsBodyHtml = false
                    };

                    mailMessage.To.Add(recipientEmail);

                    await client.SendMailAsync(mailMessage);
                }

                _logger.LogInformation($"Help email sent from {request.Email} to {recipientEmail}");
                return Ok(ApiResponse<string>.Ok("Email đã được gửi thành công! Chúng tôi sẽ phản hồi sớm nhất có thể."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email trợ giúp");
                return StatusCode(500, ApiResponse<string>.Fail($"Lỗi khi gửi email: {ex.Message}"));
            }
        }

        public class SendHelpEmailRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }
    }
}

