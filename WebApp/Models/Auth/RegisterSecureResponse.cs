namespace WebApp.Models.Auth
{
    public class RegisterSecureResponse
    {
        public bool Success { get; set; }
        public string? Type { get; set; } // "file" or null
        public string? PrivateKeyBase64 { get; set; }
        public string? FileName { get; set; }
        public string? Message { get; set; }
    }
}
