namespace FamilyChat.Server.Domain.Entities
{
    public class Chat : Entity
    {
        public List<User> Participants { get; } = new List<User>();
        public List<Message> Messages { get; } = new List<Message>();
    }
}
