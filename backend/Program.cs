using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using YouthGroupAttendance.Api.Authentication;
using YouthGroupAttendance.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Load local secrets (not tracked by git)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure API key authentication
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme, options =>
        {
            options.ApiKey = builder.Configuration["ApiKey"] ?? "changeme";
        });

builder.Services.AddAuthorization();

// Configure Entity Framework with SQLite
var connectionString = builder.Configuration.GetConnectionString("YouthGroupDb");
builder.Services.AddDbContext<YouthGroupContext>(options =>
    options.UseSqlite(connectionString));

// Configure CORS for Blazor frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Apply pending migrations and create the database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<YouthGroupContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("Development environment detected.");
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Youth Group Attendance API")
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
               .WithPreferredScheme("ApiKey")
               .WithApiKeyAuthentication(x => { x.Token = builder.Configuration["ApiKey"] ?? "changeme"; });
    });
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Delete the database when the host fully shuts down (e.g. Ctrl+C).
var dbPath = ParseSqliteDataSource(connectionString);
if (dbPath != null && app.Environment.IsDevelopment())
{
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopped.Register(() =>
    {
        Console.WriteLine("Application stopped. Deleting database...");
        DeleteSqliteDatabase(dbPath, app.Environment.IsDevelopment());
    });
}

app.Run();

static void DeleteSqliteDatabase(string dbPath, bool isDevelopment)
{
    if (!isDevelopment) return;

    foreach (var suffix in new[] { "", "-wal", "-shm" })
    {
        var path = dbPath + suffix;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

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
    {
        path = path[..semicolon];
    }

    return string.IsNullOrWhiteSpace(path) ? null : path;
}
