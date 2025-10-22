using CMCS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<MonthlyClaim> MonthlyClaims { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Unique: one claim per IC per month
            builder.Entity<MonthlyClaim>()
                .HasIndex(c => new { c.IcUserId, c.MonthKey })
                .IsUnique();

            // Document configuration
            builder.Entity<Document>(e =>
            {
                e.HasKey(d => d.Id);

                e.Property(d => d.FileName).HasMaxLength(260).IsRequired();
                e.Property(d => d.ContentType).HasMaxLength(128).IsRequired();
                e.Property(d => d.StoragePath).HasMaxLength(512).IsRequired();

                // Explicit, single relationship: Document (many) -> MonthlyClaim (one)
                e.HasOne(d => d.MonthlyClaim)
                 .WithMany(c => c.Documents)
                 .HasForeignKey(d => d.MonthlyClaimId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Safety: if EF cached a shadow prop, ignore it explicitly
                e.Ignore("MonthlyClaimId1");
            });
        }
    }
}
