using Microsoft.AspNetCore.Identity; // Data/SeedIdentity.cs
using Microsoft.Extensions.DependencyInjection;

namespace CMCS.Data
{
    public static class SeedIdentity
    {
        private static readonly string[] Roles = new[] { "IC", "MR", "CO" };

        public static async Task InitializeAsync(IServiceProvider services, ILogger logger = null!)
        {
            var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Create roles
            foreach (var r in Roles)
            {
                if (!await roleMgr.RoleExistsAsync(r))
                {
                    var roleResult = await roleMgr.CreateAsync(new IdentityRole(r));
                    if (!roleResult.Succeeded)
                    {
                        var msg = $"Seeding role '{r}' failed: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}";
                        logger?.LogError(msg);
                        throw new Exception(msg);
                    }
                    logger?.LogInformation("Seeded role {Role}", r);
                }
            }

            // Seed users
            await EnsureUserAsync(userMgr, logger, "ic@example.com", "P@ssw0rd!", "IC");
            await EnsureUserAsync(userMgr, logger, "mr@example.com", "P@ssw0rd!", "MR");
            await EnsureUserAsync(userMgr, logger, "co@example.com", "P@ssw0rd!", "CO");
        }

        private static async Task EnsureUserAsync(
            UserManager<ApplicationUser> userMgr,
            ILogger logger,
            string email,
            string password,
            string role)
        {
            var user = await userMgr.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var create = await userMgr.CreateAsync(user, password);
                if (!create.Succeeded)
                {
                    var msg = $"Seeding user '{email}' failed: {string.Join("; ", create.Errors.Select(e => e.Description))}";
                    logger?.LogError(msg);
                    throw new Exception(msg);
                }
                logger?.LogInformation("Seeded user {Email}", email);
            }

            if (!await userMgr.IsInRoleAsync(user, role))
            {
                var addRole = await userMgr.AddToRoleAsync(user, role);
                if (!addRole.Succeeded)
                {
                    var msg = $"Adding user '{email}' to role '{role}' failed: {string.Join("; ", addRole.Errors.Select(e => e.Description))}";
                    logger?.LogError(msg);
                    throw new Exception(msg);
                }
                logger?.LogInformation("Added user {Email} to role {Role}", email, role);
            }
        }
    }
}
