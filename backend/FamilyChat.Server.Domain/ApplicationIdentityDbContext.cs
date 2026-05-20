using FamilyChat.Server.Domain.Entities;
using FamilyChat.Server.Domain.EntityConfiguration;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace FamilyChat.Server.Domain
{
    public class ApplicationIdentityDbContext : IdentityDbContext<User>
    {

        public DbSet<User> ChatUsers { get; set; }
        public DbSet<Chat> Chats { get; set; }

        public ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options) :
            base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(typeof(UserEntityTypeConfiguration).Assembly);
        }

    }
}
