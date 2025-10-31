namespace WebApp.Models
{
    public class UserRoleViewModel
    {
        public List<RoleUserDto> Users { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
    }
}


