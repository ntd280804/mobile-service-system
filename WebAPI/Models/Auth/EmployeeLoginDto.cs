namespace WebAPI.Models.Auth
{
    public class EmployeeLoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Platform { get; set; } // "WEB" hoặc "MOBILE"
    }
}
