namespace WebApp.Models.Auth
{
    public class QrLoginCompleteDto
    {
        public string? Username { get; set; }
        public string? Roles { get; set; }
        public string? Token { get; set; }
        public string? SessionId { get; set; }
    }
}


