using JyotiIyerCPA.Data;
using JyotiIyerCPA.Models;
using JyotiIyerCPA.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        // Enable retry on transient failures (required for Azure SQL)
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// Add Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    var isDev = builder.Environment.IsDevelopment();
    if (isDev)
    {
        // Relaxed for development convenience
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 4;
    }
    else
    {
        // Production policy: "usual secure" per requirements
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
    }

    // Lockout settings (10 minutes, per requirements)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Identity token lifespan (e.g., invite/reset tokens)
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.LoginPath = "/Home/ClientPortal"; // Redirect unauthenticated users to Client Portal login
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Configure security stamp validation interval (validates session every 30 seconds)
// When a user logs in on another device/tab, the security stamp changes and old sessions are invalidated
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromSeconds(30);
});

// Register the email service (custom interface)
builder.Services.AddTransient<JyotiIyerCPA.Services.IEmailSender, JyotiIyerCPA.Services.EmailService>();

// File storage options and encrypted storage service
builder.Services.Configure<JyotiIyerCPA.Options.FileStorageOptions>(builder.Configuration.GetSection("Storage"));

// Choose storage provider based on configuration (default: Local, or Azure if configured)
var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "Local";
if (storageProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<JyotiIyerCPA.Services.IFileStorage, JyotiIyerCPA.Services.AzureBlobFileStorage>();
}
else
{
    builder.Services.AddSingleton<JyotiIyerCPA.Services.IFileStorage, JyotiIyerCPA.Services.LocalEncryptedFileStorage>();
}

// Antiforgery for JSON fetches
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "RequestVerificationToken";
});

// Rate limiting for login/invite endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = 10; // 10 requests per minute
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Ensure database and roles exist. Admin user/password will be provisioned via DB, not in code.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var database = db.Database;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        // Only auto-migrate in Development for fast iteration
        if (app.Environment.IsDevelopment())
        {
            logger.LogInformation("Development mode: Applying pending migrations...");
            if (database.GetMigrations().Any())
            {
                await database.MigrateAsync();
            }
            else
            {
                await database.EnsureCreatedAsync();
            }
        }
        else
        {
            // Production: Verify connection only (migrations applied manually via SQL)
            var canConnect = await database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogError("Cannot connect to production database!");
                throw new InvalidOperationException("Database connection failed");
            }
            logger.LogInformation("Production mode: Database connection verified. Migrations managed manually.");
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("Client"))
            await roleManager.CreateAsync(new IdentityRole("Client"));

        // Optional: seed admin from environment variables if provided
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seedEmail = Environment.GetEnvironmentVariable("Seed__AdminEmail");
        var seedPassword = Environment.GetEnvironmentVariable("Seed__AdminPassword");
        if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword))
        {
            Console.WriteLine($"[Seed] Attempting to seed admin user: {seedEmail}");
            var existing = await userManager.FindByEmailAsync(seedEmail);
            if (existing == null)
            {
                var admin = new ApplicationUser { UserName = seedEmail, Email = seedEmail, EmailConfirmed = true, FirstName = "Admin", LastName = "User", ClientType = "Admin", ProfilePictureUrl = "" };
                var res = await userManager.CreateAsync(admin, seedPassword);
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                    Console.WriteLine($"[Seed] Admin user created successfully: {seedEmail}");
                }
                else
                {
                    Console.WriteLine($"[Seed] Failed to create admin user. Errors:");
                    foreach (var error in res.Errors)
                        Console.WriteLine($"  - {error.Code}: {error.Description}");
                }
            }
            else
            {
                Console.WriteLine($"[Seed] Admin user already exists: {seedEmail}");
                // Ensure existing user has Admin role
                if (!await userManager.IsInRoleAsync(existing, "Admin"))
                {
                    await userManager.AddToRoleAsync(existing, "Admin");
                    Console.WriteLine($"[Seed] Added Admin role to existing user: {seedEmail}");
                }
            }
        }
        else
        {
            Console.WriteLine("[Seed] No Seed__AdminEmail/Seed__AdminPassword environment variables found.");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogError(ex, "Error ensuring database/roles.");
    }
}

// Optional health endpoint to verify 'Documents' table and migration status
app.MapGet("/health/db/documents", async (ApplicationDbContext db) =>
{
    var applied = db.Database.GetAppliedMigrations().ToArray();
    var pending = db.Database.GetPendingMigrations().ToArray();

    var exists = false;
    try
    {
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Documents') THEN 1 ELSE 0 END";
        var result = await cmd.ExecuteScalarAsync();
        exists = Convert.ToInt32(result) == 1;
    }
    catch
    {
        exists = false;
    }

    return Results.Json(new
    {
        tableExists = exists,
        appliedMigrations = applied,
        pendingMigrations = pending
    });
});

app.Run();
