namespace WebAPI.Models.Security
{
    public class EncryptForClientResponse
    {
        public string? EncryptedKeyBlockBase64 { get; set; }
        public string? CipherDataBase64 { get; set; }
    }
}
