namespace WebApp.Models
{
    public class UserProfileViewModel
    {
        public List<ProfileUserDto> Users { get; set; } = new();
        public List<ProfileDto> Profiles { get; set; } = new();
    }
}


