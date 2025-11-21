using CMCS.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// === Logging ===
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// === MVC / Controllers ===
builder.Services.AddControllersWithViews();

// === EF Core / SQLite ===
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// === Session ===
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// === Swagger / OpenAPI ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// === HTTP pipeline ===
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

// Swagger UI – left on in all environments so it’s easy to demo
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CMCS API v1");
    c.RoutePrefix = "swagger"; // URL: /swagger
});

// === MVC routing ===
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// === Seed default users ===
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("SeedUsers");

    try
    {
        await SeedUsers.InitializeAsync(sp, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding users.");
        throw;
    }
}

app.Run();
