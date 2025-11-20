using System;

namespace WebApp.Models.Auth
{
    public class WebToMobileQrCreateResponse
    {
        public string QrLoginId { get; set; }
        public string Code { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class WebToMobileQrStatusResponse
    {
        public string QrLoginId { get; set; }
        public string Code { get; set; }
        public WebToMobileQrStatus Status { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public enum WebToMobileQrStatus
    {
        Pending = 0,
        Confirmed = 1,
        Expired = 2
    }
}


