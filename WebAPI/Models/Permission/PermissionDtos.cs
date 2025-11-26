namespace WebAPI.Models.Permission
{
    public class AssignProfileRequest
    {
        public string Username { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
    }

    public class AssignRoleRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    public class RevokeRoleRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    public class CreateProfileRequest
    {
        public string ProfileName { get; set; } = string.Empty;
        public string IdleTime { get; set; } = "UNLIMITED";
        public string ConnectTime { get; set; } = "UNLIMITED";
        public string FailedLogin { get; set; } = "UNLIMITED";
        public string LockTime { get; set; } = "UNLIMITED";
        public string InactiveAccountTime { get; set; } = "UNLIMITED";
    }

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
