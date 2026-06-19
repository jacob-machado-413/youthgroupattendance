using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using YouthGroupAttendance.Api.Authentication;
using YouthGroupAttendance.Api.Data;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (!builder.Environment.IsEnvironment("Test"))
        {
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
        }

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.DefaultScheme, options =>
                {
                    options.ApiKey = builder.Configuration["ApiKey"]
                                  ?? Environment.GetEnvironmentVariable("YOUTH_GROUP_API_KEY")
                                  ?? "changeme";
                });

        builder.Services.AddAuthorization();

        var connectionString = builder.Configuration.GetConnectionString("YouthGroupDb");
        builder.Services.AddDbContext<YouthGroupContext>(options =>
            options.UseSqlite(connectionString));

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("https://localhost:5001", "http://localhost:5000", "http://localhost:5091")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YouthGroupContext>();
            db.Database.EnsureCreated();
        }

        // Serve static frontend files in all non-dev environments
        if (!app.Environment.IsDevelopment())
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine("Development environment detected.");
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("Youth Group Attendance API")
                       .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                       .AddPreferredSecuritySchemes("ApiKey");
            });
        }

        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // SPA fallback: must come after controllers so /api/* routes take priority
        if (!app.Environment.IsDevelopment())
        {
            app.MapFallbackToFile("index.html");
        }

        var dbPath = ParseSqliteDataSource(connectionString);
        if (dbPath != null && app.Environment.IsDevelopment())
        {
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopped.Register(() =>
            {
                Console.WriteLine("Application stopped. Deleting database...");
            });
        }

        app.Run();


    }
#pragma warning disable IDE0051
    // The code that's violating the rule is on this line.

    static void DeleteSqliteDatabase(string dbPath)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = dbPath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
#pragma warning restore IDE0051



    static string? ParseSqliteDataSource(string? connStr)
    {
        if (connStr == null) return null;

        const string prefix = "Data Source=";
        var index = connStr.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;

        var start = index + prefix.Length;
        var path = connStr[start..].Trim();

        var semicolon = path.IndexOf(';');
        if (semicolon >= 0)
            path = path[..semicolon];

        return string.IsNullOrWhiteSpace(path) ? null : path;
    }
}
