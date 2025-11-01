using System.Text.Json.Serialization;

namespace WebApp.Models.Auth
{
    public class CustomerLoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Result { get; set; }
        [JsonPropertyName("roles")]
        public string roles { get; set; }
        public string token { get; set; }
        public string? message { get; set; }
        public string? sessionId { get; set; }
    }
}

