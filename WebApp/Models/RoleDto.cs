using System.Text.Json.Serialization;

namespace WebApp.Models
{
    public class RoleDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("grantedRole")]
        public string GrantedRole { get; set; }
    }
}


