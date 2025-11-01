namespace WebApp.Models.Auth
{
    public class LoginResultEnvelope
    {
        public string? Username { get; set; }
        public string? Roles { get; set; }
        public string? Result { get; set; }
        public string? SessionId { get; set; }
        public string? Token { get; set; }
        public string? Message { get; set; }
    }
}

