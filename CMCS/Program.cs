using Microsoft.AspNetCore.Identity;            // Identity 
using Microsoft.EntityFrameworkCore;            // AddDbContext, UseSqlite
using Microsoft.Extensions.Logging;            // ILogger<T>
using CMCS.Data;                                // ApplicationDbContext, ApplicationUser

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// EF Core + SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // Make testing easy:
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.Lockout.AllowedForNewUsers = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // BEFORE UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

// Ensure DB is migrated, then seed roles/users (with logging)
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    // 1) Ensure DB exists and schema is up to date
    var db = sp.GetRequiredService<CMCS.Data.ApplicationDbContext>();
    await db.Database.MigrateAsync();

    // 2) Seed roles/users (with logging)
    var logger = sp.GetRequiredService<ILogger<Program>>();
    await CMCS.Data.SeedIdentity.InitializeAsync(sp, logger);
}
app.Run();