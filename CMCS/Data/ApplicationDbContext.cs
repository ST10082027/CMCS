using CMCS.Models;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserAccount> Users { get; set; } = null!;
        public DbSet<MonthlyClaim> MonthlyClaims { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // === UserAccount ===
            builder.Entity<UserAccount>(e =>
            {
                e.HasKey(u => u.Id);

                e.Property(u => u.UserName)
                    .IsRequired()
                    .HasMaxLength(256);

                e.Property(u => u.Email)
                    .IsRequired()
                    .HasMaxLength(256);

                e.Property(u => u.Role)
                    .IsRequired()
                    .HasMaxLength(64);

                e.Property(u => u.PasswordHash)
                    .IsRequired();

                e.Property(u => u.FirstName)
                    .HasMaxLength(50);

                e.Property(u => u.LastName)
                    .HasMaxLength(50);

                e.Property(u => u.HourlyRate)
                    .HasColumnType("decimal(10,2)");

                e.Property(u => u.PhoneNumber)
                    .HasMaxLength(20);

                e.HasIndex(u => u.UserName).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
            });

            // === MonthlyClaim ===
            builder.Entity<MonthlyClaim>(e =>
            {
                e.HasKey(c => c.Id);

                e.Property(c => c.MonthKey)
                    .IsRequired()
                    .HasMaxLength(7);

                e.Property(c => c.Hours)
                    .HasColumnType("decimal(7,2)");

                e.Property(c => c.Rate)
                    .HasColumnType("decimal(10,2)");

                e.Property(c => c.ManagerRemark)
                    .HasMaxLength(2000);

                e.Property(c => c.Notes)
                    .HasMaxLength(2000);

                // Lecturer
                e.HasOne(c => c.IcUser)
                    .WithMany()
                    .HasForeignKey(c => c.IcUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Programme Coordinator
                e.HasOne(c => c.CoordinatorUser)
                    .WithMany()
                    .HasForeignKey(c => c.CoordinatorUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // === Document ===
            builder.Entity<Document>(e =>
            {
                e.HasKey(d => d.Id);

                e.Property(d => d.FileName)
                    .HasMaxLength(255)
                    .IsRequired();

                e.Property(d => d.ContentType)
                    .HasMaxLength(128)
                    .IsRequired();

                e.Property(d => d.StoragePath)
                    .HasMaxLength(512)
                    .IsRequired();

                e.HasOne(d => d.MonthlyClaim)
                    .WithMany(c => c.Documents)
                    .HasForeignKey(d => d.MonthlyClaimId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
