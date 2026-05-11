using FamilyChat.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyChat.Server.Domain
{
    public class ApplicationIdentityDbContext : IdentityDbContext<ChatUser>
    {

        public DbSet<ChatUser> ChatUsers { get; set; }

        public ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options) :
            base(options)
        { }


    }
}
