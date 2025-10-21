using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CMCS.Data
{
    public static class SeedIdentity
    {
        private static readonly string[] Roles = new[] { "IC", "MR", "CO" };

        // Demo users
        private static readonly (string Email, string Role, string First, string Last)[] Users =
        {
            ("ic@example.com", "IC", "Ivy", "Contractor"),
            ("mr@example.com", "MR", "Mandla", "Reviewer"),
            ("co@example.com", "CO", "Cora", "Officer"),
        };

        private const string DefaultPassword = "P@ssw0rd!";

        public static async Task InitializeAsync(IServiceProvider services, ILogger logger = null!)
        {
            var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Roles
            foreach (var r in Roles)
            {
                if (!await roleMgr.RoleExistsAsync(r))
                {
                    var created = await roleMgr.CreateAsync(new IdentityRole(r));
                    if (!created.Succeeded)
                    {
                        var msg = $"Failed creating role '{r}': {string.Join("; ", created.Errors.Select(e => e.Description))}";
                        logger?.LogError(msg);
                        throw new Exception(msg);
                    }
                    logger?.LogInformation("Created role {Role}", r);
                }
            }

            // Users
            foreach (var (email, role, first, last) in Users)
            {
                var user = await userMgr.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FirstName = first,
                        LastName = last
                    };

                    var created = await userMgr.CreateAsync(user, DefaultPassword);
                    if (!created.Succeeded)
                    {
                        var msg = $"Failed creating user '{email}': {string.Join("; ", created.Errors.Select(e => e.Description))}";
                        logger?.LogError(msg);
                        throw new Exception(msg);
                    }
                    logger?.LogInformation("Created user {Email}", email);
                }

                if (!await userMgr.IsInRoleAsync(user, role))
                {
                    var addRole = await userMgr.AddToRoleAsync(user, role);
                    if (!addRole.Succeeded)
                    {
                        var msg = $"Failed adding user '{email}' to role '{role}': {string.Join("; ", addRole.Errors.Select(e => e.Description))}";
                        logger?.LogError(msg);
                        throw new Exception(msg);
                    }
                    logger?.LogInformation("Added user {Email} to role {Role}", email, role);
                }
            }
        }
    }
}
