namespace WebAPI.Models.Auth
{
    public class CustomerLoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public class CustomerLoginResult
    {
        public string? Username { get; set; }
        public string? Roles { get; set; }
        public string? Result { get; set; }
        public string? SessionId { get; set; }
        public string? Token { get; set; }
        public string? Message { get; set; }
    }

    public class CustomerRegisterDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class CustomerQrLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string? Platform { get; set; }
    }
}
