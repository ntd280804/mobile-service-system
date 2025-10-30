namespace WebAPI.Models.Auth
{
    public class CustomerLoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Platform { get; set; }
    }
}
