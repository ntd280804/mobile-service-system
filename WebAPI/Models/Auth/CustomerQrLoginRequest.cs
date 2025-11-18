namespace WebAPI.Models.Auth
{
    public class CustomerQrLoginRequest
    {
        public string Username { get; set; } = default!;
        public string? Platform { get; set; }
    }
}


