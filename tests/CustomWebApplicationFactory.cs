using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;

namespace YouthGroupAttendance.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath;

    public CustomWebApplicationFactory()
    {
        _dbPath = $"TestDb_{Guid.NewGuid():N}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the API key via environment variable for tests (always takes priority)
        Environment.SetEnvironmentVariable("YOUTH_GROUP_API_KEY", "test-api-key");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<YouthGroupContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<YouthGroupContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));
        });

        builder.UseEnvironment("Test");
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<YouthGroupContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        try
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var path = _dbPath + suffix;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        catch { }

        await base.DisposeAsync();
    }
}
