namespace WebApp.Models.Permission
{
    public class ProfileDto
    {
        public string ProfileName { get; set; }
        public string IdleTime { get; set; }
        public string ConnectTime { get; set; }
        public string FailedLogin { get; set; }
        public string LockTime { get; set; }
        public string InactiveAccountTime { get; set; }
    }
}

