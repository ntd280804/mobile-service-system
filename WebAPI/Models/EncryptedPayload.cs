namespace WebAPI.Models
{
    public class EncryptedPayload
    {
        public string EncryptedKeyBlock { get; set; } // Base64 enc AES key+IV
        public string CipherData { get; set; } // Base64
        public string ClientPublicKey { get; set; } // Base64 (optional, client gửi cho server nếu chưa có)
    }
}
