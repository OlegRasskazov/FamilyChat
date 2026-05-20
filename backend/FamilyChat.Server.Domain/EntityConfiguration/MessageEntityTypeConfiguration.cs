using FamilyChat.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyChat.Server.Domain.EntityConfiguration
{
    internal class MessageEntityTypeConfiguration: IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.UserId);
        }
    }
}
