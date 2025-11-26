namespace WebAPI.Models.Security
{
    public class EncryptForClientRequest
    {
        public string? ClientId { get; set; }
        public string? Plaintext { get; set; }
    }
}
