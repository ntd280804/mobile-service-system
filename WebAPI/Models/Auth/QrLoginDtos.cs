using System;

namespace WebAPI.Models.Auth
{
    public class QrLoginCreateResponse
    {
        public string QrLoginId { get; set; } = default!;
        public string Code { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class QrLoginConfirmRequest
    {
        public string Code { get; set; } = default!;
    }

    public class QrLoginStatusResponse
    {
        public string QrLoginId { get; set; } = default!;
        public string Code { get; set; } = default!;
        public QrLoginStatus Status { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        // Chỉ có khi Status = Confirmed
        public string? Username { get; set; }
        public string? Roles { get; set; }
        public string? WebToken { get; set; }
        public string? WebSessionId { get; set; }
    }

    public enum QrLoginStatus
    {
        Pending = 0,
        Confirmed = 1,
        Expired = 2
    }
}


