namespace WebAPI.Models.Permission
{
    public class UpdateProfileRequest
    {
        public string ProfileName { get; set; } = string.Empty;
        public string? IdleTime { get; set; }
        public string? ConnectTime { get; set; }
        public string? FailedLogin { get; set; }
        public string? LockTime { get; set; }
        public string? InactiveAccountTime { get; set; }
    }
}

