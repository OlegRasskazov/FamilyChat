using Microsoft.AspNetCore.Identity;

namespace FamilyChat.Server.Domain.Entities
{
    public class User : IdentityUser
    {
        public string Nickname { get; set; }
        public List<User> Friends { get; } = new List<User>();
        public List<Chat> Chats { get; } = new List<Chat>();

    }
}
