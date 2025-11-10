using System;

namespace WebAPI.Models.Security
{
    public class EncryptedPayload
    {
        public string? EncryptedKeyBlockBase64 { get; set; }
        public string? CipherDataBase64 { get; set; }
    }
}


