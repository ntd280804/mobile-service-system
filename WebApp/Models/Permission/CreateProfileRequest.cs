namespace WebApp.Models.Permission
{
    public class CreateProfileRequest
    {
        public string ProfileName { get; set; } = string.Empty;
        public string FailedLogin { get; set; } = "UNLIMITED";
        public string LifeTime { get; set; } = "UNLIMITED";
        public string GraceTime { get; set; } = "UNLIMITED";
        public string LockTime { get; set; } = "UNLIMITED";
    }
}

