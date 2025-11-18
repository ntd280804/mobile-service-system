namespace WebApp.Models.Permission
{
    public class CreateProfileRequest
    {
        public string ProfileName { get; set; } = string.Empty;
        public string IdleTime { get; set; } = "UNLIMITED";
        public string ConnectTime { get; set; } = "UNLIMITED";
        public string FailedLogin { get; set; } = "UNLIMITED";
        public string LockTime { get; set; } = "UNLIMITED";
        public string InactiveAccountTime { get; set; } = "UNLIMITED";
    }
}

