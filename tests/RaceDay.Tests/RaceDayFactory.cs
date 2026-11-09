using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>
/// Boots the real API in memory for each test class, swapping SQL Server for an in-memory database
/// so tests are fast, isolated and need no external services (they also run in GitHub Actions).
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "RACEDAY-ORG-2026"; // same as appsettings.json, so tests pass even if the override is not applied

    private readonly string _dbName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:OrganiserCode"] = OrganiserCode
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RaceDayContext>(options => options.UseInMemoryDatabase(_dbName));

            // Never talk to Azure from tests: use an in-memory fake of the image storage.
            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage, InMemoryImageStorage>();
        });
    }
}
//ST10486532
//Author: Gift Tshwaane