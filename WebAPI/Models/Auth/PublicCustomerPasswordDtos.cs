namespace WebAPI.Models.Auth
{
    public class ForgotPasswordGenerateOtpDto
    {
        public string Email { get; set; } = string.Empty;
    }
    public class ForgotPasswordResetDto
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
    public class ForgotPasswordOtpResponse
    {
        public string Otp { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
