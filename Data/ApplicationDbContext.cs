using JyotiIyerCPA.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JyotiIyerCPA.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<JyotiIyerCPA.Models.Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Add any additional configurations here
            // For example:
            // builder.Entity<ApplicationUser>().HasIndex(u => u.ClientType);
            builder.Entity<JyotiIyerCPA.Models.Document>()
                .HasIndex(d => new { d.OwnerUserId, d.UploadedAt });
        }
    }
}
