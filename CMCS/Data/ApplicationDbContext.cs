using CMCS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<MonthlyClaim> MonthlyClaims { get; set; } = default!;
        public DbSet<Document> Documents { get; set; } = default!; // <-- add if you’re using Document

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique: one claim per IC per month
            modelBuilder.Entity<MonthlyClaim>()
                .HasIndex(c => new { c.IcUserId, c.MonthKey })
                .IsUnique();

            // Optional: if you want a FK from Document -> MonthlyClaim
            modelBuilder.Entity<Document>()
                .HasOne<MonthlyClaim>()
                .WithMany()
                .HasForeignKey(d => d.MonthlyClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
