using System.Net;
using System.Net.Mail;

namespace WebAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpEmailAsync(string recipientEmail, string otp)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var senderEmail = _configuration["Email:SenderEmail"];
                var senderPassword = _configuration["Email:SenderPassword"];

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
                {
                    _logger.LogError("Email configuration is missing");
                    return false;
                }

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(senderEmail, "HealthCare"),
                        Subject = "Mã OTP đặt lại mật khẩu",
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(recipientEmail);

                    // HTML body với OTP
                    mailMessage.Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                                <h2 style='color: #007bff;'>Đặt lại mật khẩu</h2>
                                <p>Xin chào,</p>
                                <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản có mail: <strong>{recipientEmail}</strong></p>
                                <p>Mã OTP của bạn là:</p>
                                <div style='background-color: #f8f9fa; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                                    <h1 style='color: #007bff; font-size: 32px; letter-spacing: 5px; margin: 0;'>{otp}</h1>
                                </div>
                                <p><strong>Lưu ý:</strong></p>
                                <ul>
                                    <li>Mã OTP có hiệu lực trong 10 phút</li>
                                    <li>Mã OTP chỉ có thể sử dụng 1 lần</li>
                                    <li>Không chia sẻ mã OTP với bất kỳ ai</li>
                                </ul>
                                <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                                <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                                <p style='color: #666; font-size: 12px;'>Email này được gửi tự động, vui lòng không trả lời.</p>
                            </div>
                        </body>
                        </html>";

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"OTP email sent successfully to {recipientEmail}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending OTP email to {recipientEmail}");
                return false;
            }
        }

        public async Task<(bool Success, string Message)> SendHelpEmailAsync(string requesterEmail, string content)
        {
            try
            {
                var recipientEmail = _configuration["Email:RecipientEmail"] ?? _configuration["Email:SenderEmail"];
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var senderEmail = _configuration["Email:SenderEmail"];
                var senderPassword = _configuration["Email:SenderPassword"];

                if (string.IsNullOrWhiteSpace(recipientEmail) ||
                    string.IsNullOrWhiteSpace(senderEmail) ||
                    string.IsNullOrWhiteSpace(senderPassword))
                {
                    const string missingConfigMessage = "Cấu hình email chưa được thiết lập.";
                    _logger.LogError(missingConfigMessage);
                    return (false, missingConfigMessage);
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(senderEmail, senderPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "HealthCare"),
                    Subject = $"Yêu cầu trợ giúp từ: {requesterEmail}",
                    Body = $"Email người gửi: {requesterEmail}\n\nNội dung:\n{content}",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(recipientEmail);
                await client.SendMailAsync(mailMessage);

                _logger.LogInformation("Help email sent from {Requester} to {Recipient}", requesterEmail, recipientEmail);
                return (true, "Email đã được gửi thành công! Chúng tôi sẽ phản hồi sớm nhất có thể.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi help email từ {Requester}", requesterEmail);
                return (false, $"Lỗi khi gửi email: {ex.Message}");
            }
        }
    }
}

