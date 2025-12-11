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
        public DbSet<JyotiIyerCPA.Models.DocumentWorkflow> DocumentWorkflows { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Add any additional configurations here
            // For example:
            // builder.Entity<ApplicationUser>().HasIndex(u => u.ClientType);
            builder.Entity<JyotiIyerCPA.Models.Document>()
                .HasIndex(d => new { d.OwnerUserId, d.UploadedAt });

            // DocumentWorkflow indexes for efficient queries
            builder.Entity<JyotiIyerCPA.Models.DocumentWorkflow>()
                .HasIndex(w => new { w.ClientUserId, w.Status });
            builder.Entity<JyotiIyerCPA.Models.DocumentWorkflow>()
                .HasIndex(w => new { w.AdminUserId, w.Status });
        }
    }
}
