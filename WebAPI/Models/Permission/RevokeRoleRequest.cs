namespace WebAPI.Models.Permission
{
    public class RevokeRoleRequest
    {
        public string UserName { get; set; }
        public string RoleName { get; set; }
    }
}
