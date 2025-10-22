using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CMCS.Data;
using CMCS.Infrastructure;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// === Console logging on ===
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// MVC
builder.Services.AddControllersWithViews();

// EF Core + SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity + Roles + Tokens
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Sign-in rules
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;

        // Password rules (relaxed for demo/testing)
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireDigit = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// log every action enter/exit
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ActionLoggingFilter>();
});

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Default route -> Login
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Migrate + seed roles/users
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    // Apply any pending migrations
    try
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error running migrations.");
        throw;
    }

    // Seed roles and default users
    try
    {
        await SeedIdentity.InitializeAsync(sp, logger);
        logger.LogInformation("Identity seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding identity.");
        throw;
    }
}

app.Run();
