using FamilyChat.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyChat.Server.Domain.EntityConfiguration
{
    internal class ChatEntityTypeConfiguration : IEntityTypeConfiguration<Chat>
    {
        public void Configure(EntityTypeBuilder<Chat> builder)
        {
            builder.HasMany(c => c.Messages)
                .WithOne()
                .HasForeignKey(m => m.ChatId);
        }
    }
}
