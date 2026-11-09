using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (Entity Framework Core, Code-First, SQL Server) ----------
builder.Services.AddDbContext<RaceDayContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

// ---------- Password hashing (PBKDF2 with a per-user salt; the plain password is never stored) ----------
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// ---------- Image storage (Azure Blob Storage; swapped for an in-memory fake in the tests) ----------
builder.Services.AddSingleton<IImageStorage, AzureBlobImageStorage>();

// ---------- Session-based authentication (cookie) with role claims ----------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDay.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;

        // This is an API: answer with status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Secure by default: anything not marked [AllowAnonymous] requires a logged-in user.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddControllers();

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "REST API for the RaceDay event management system (running, walking and cycling events).\n\n" +
            "**How to test:** 1) POST /api/auth/register  2) POST /api/auth/login  3) call any endpoint with 'Try it out'. " +
            "Login sets a session cookie that Swagger UI sends automatically. " +
            "Each endpoint lists the role required. Use POST /api/auth/logout to switch users."//ST10486532
                                                                                               //Author: Gift Tshwaane
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// ---------- Create / update the database on start-up ----------
// This submission contains the Part 2 schema plus the Part 3 image columns. The
// supplied migration folder does not contain the original InitialCreate migration,
// so calling Migrate() on a fresh database would try to ALTER tables that do not
// exist. EnsureCreated builds a fresh database from the complete EF model; for an
// existing Part 2 database, the idempotent SQL below adds only the two new columns.
// In Docker SQL Server may take a little while to accept connections, so retry there.
var inContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var maxAttempts = inContainer ? 20 : 1;

for (var attempt = 1; ; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        if (db.Database.IsRelational())
        {
            db.Database.EnsureCreated();
            db.Database.ExecuteSqlRaw("""
                IF OBJECT_ID(N'dbo.Events', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Events', N'BannerBlobName') IS NULL
                    ALTER TABLE dbo.Events ADD BannerBlobName varchar(255) NULL;
                IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Users', N'ProfilePictureBlobName') IS NULL
                    ALTER TABLE dbo.Users ADD ProfilePictureBlobName varchar(255) NULL;
                """);
        }
        break;
    }
    catch (Exception ex) when (attempt < maxAttempts)
    {
        app.Logger.LogWarning(ex, "Database not ready (attempt {Attempt} of {Max}). Retrying in 5 seconds...", attempt, maxAttempts);
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.DocumentTitle = "RaceDay API";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();
app.MapControllers();

app.Run();

// Makes the Program class visible to the integration tests (WebApplicationFactory<Program>).
public partial class Program { }

//ST10486532
//Author: Gift Tshwaane