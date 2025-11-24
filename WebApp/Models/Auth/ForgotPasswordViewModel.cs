using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Auth
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email nhận OTP.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email nhận mã OTP")]
        public string? Email { get; set; }

        [Display(Name = "Mã OTP")]
        public string? Otp { get; set; }

        [Display(Name = "Mật khẩu mới")]
        public string? NewPassword { get; set; }

        [Display(Name = "Xác nhận mật khẩu")]
        public string? ConfirmPassword { get; set; }

        public bool OtpSent { get; set; }
        public string? OtpStatusMessage { get; set; }
    }
}

