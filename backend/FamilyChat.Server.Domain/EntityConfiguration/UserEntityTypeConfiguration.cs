using FamilyChat.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyChat.Server.Domain.EntityConfiguration
{
    internal class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasMany(u => u.Chats)
                    .WithMany(c => c.Participants)
                    .UsingEntity<UserChat>()
                    .HasIndex(e => new { e.UserId, e.ChatId }).IsUnique();
            builder.HasMany(u => u.Friends)
                    .WithMany();
        }
    }
}
