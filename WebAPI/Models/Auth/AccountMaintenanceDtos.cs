namespace WebAPI.Models.Auth
{
    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UnlockEmployeeDto
    {
        public string Username { get; set; } = string.Empty;
    }

    public class UnlockCustomerDto
    {
        public string Phone { get; set; } = string.Empty;
    }
}