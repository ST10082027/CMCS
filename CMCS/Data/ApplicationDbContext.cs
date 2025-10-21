using Microsoft.AspNetCore.Identity.EntityFrameworkCore;   // requires Microsoft.AspNetCore.Identity.EntityFrameworkCore
using Microsoft.EntityFrameworkCore;                       // requires Microsoft.EntityFrameworkCore
using CMCS.Data;

namespace CMCS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Add DbSet<T> here later, e.g.:
        // public DbSet<MonthlyClaim> MonthlyClaims { get; set; } = default!;
        public DbSet<CMCS.Models.MonthlyClaim> MonthlyClaims { get; set; } = default!;
    }
}
