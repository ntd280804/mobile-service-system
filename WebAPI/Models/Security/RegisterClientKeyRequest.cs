namespace WebAPI.Models.Security
{
    public class RegisterClientKeyRequest
    {
        public string? ClientId { get; set; }
        public string? ClientPublicKeyBase64 { get; set; }
    }
}
