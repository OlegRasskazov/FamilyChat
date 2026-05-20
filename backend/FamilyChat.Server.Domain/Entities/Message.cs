namespace FamilyChat.Server.Domain.Entities
{
    public class Message: Entity
    {
        public string? Text { get; set; }
        public string? Image { get; set; }
        public string? Video{ get; set; }

        public string UserId { get; set; }
        public Guid ChatId { get; set; }
    }
}
