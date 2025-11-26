using System;

namespace WebAPI.Models.Auth
{
    public class WebToMobileQrCreateResponse
    {
        public string QrLoginId { get; set; } = default!;
        public string Code { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class WebToMobileQrStatusResponse
    {
        public string QrLoginId { get; set; } = default!;
        public string Code { get; set; } = default!;
        public WebToMobileQrStatus Status { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public enum WebToMobileQrStatus
    {
        Pending = 0,
        Confirmed = 1,
        Expired = 2
    }

    public class WebToMobileQrConfirmRequest
    {
        public string Code { get; set; } = default!;
        public string? Platform { get; set; }
    }

    public class WebToMobileQrConfirmResponse
    {
        public string Username { get; set; } = default!;
        public string Roles { get; set; } = default!;
        public string Token { get; set; } = default!;
        public string SessionId { get; set; } = default!;
    }
}

