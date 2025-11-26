namespace WebAPI.Models.Public
{
    public class SendHelpEmailRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
