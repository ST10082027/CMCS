using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMCS.Data
{
    public static class SeedUsers
    {
        private static readonly (string Email, string UserName, string Role, string First, string Last, decimal Rate)[] Users =
        {
            ("lecturer@example.com",    "lecturer",    "Lecturer",        "Lerato",   "Lecturer",    650m),
            ("coordinator@example.com", "coordinator", "Coordinator",     "Peter",    "Coordinator", 0m),
            ("manager@example.com",     "manager",     "AcademicManager", "Amira",    "Manager",     0m),
            ("hr@example.com",          "hr",          "HR",              "Hannah",   "HR",          0m)
        };

        private const string DefaultPassword = "P@ssw0rd!";

        public static async Task InitializeAsync(IServiceProvider services, ILogger logger = null!)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.MigrateAsync();

            if (await db.Users.AnyAsync())
            {
                logger?.LogInformation("Users already seeded.");
                return;
            }

            foreach (var u in Users)
            {
                var user = new UserAccount
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = u.Email,
                    UserName = u.UserName,
                    Role = u.Role,
                    FirstName = u.First,
                    LastName = u.Last,
                    HourlyRate = u.Rate,
                    PasswordHash = PasswordHelper.HashPassword(DefaultPassword)
                };

                db.Users.Add(user);
            }

            await db.SaveChangesAsync();
            logger?.LogInformation("Seeded default UserAccount records.");
        }
    }
}
