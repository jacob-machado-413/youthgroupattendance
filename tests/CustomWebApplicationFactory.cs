using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;

namespace YouthGroupAttendance.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory that uses a test-specific SQLite database.
/// The database file is cleaned up after all tests in the class complete.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath;

    public CustomWebApplicationFactory()
    {
        _dbPath = $"TestDb_{Guid.NewGuid():N}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<YouthGroupContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test DbContext with a unique SQLite file
            services.AddDbContext<YouthGroupContext>(options =>
            {
                options.UseSqlite($"Data Source={_dbPath}");
            });
        });

        // Use test environment so the Scalar UI / OpenAPI don't interfere
        builder.UseEnvironment("Test");
    }

    public async Task InitializeAsync()
    {
        // Create the database
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<YouthGroupContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        try
        {
            // Clean up the SQLite file
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }

        await base.DisposeAsync();
    }
}
