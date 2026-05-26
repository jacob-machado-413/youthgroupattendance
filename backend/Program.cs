using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using YouthGroupAttendance.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Delete the database when the host fully shuts down (e.g. Ctrl+C).
// EnsureCreated() will recreate it fresh on the next startup.
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

/// <summary>
/// Deletes a SQLite database file along with its WAL (-wal) and SHM (-shm) companion files.
/// </summary>
static void DeleteSqliteDatabase(string dbPath, bool isDevelopment = false)
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

/// <summary>
/// Parses the data source path from a SQLite connection string like "Data Source=path/to/db.db".
/// </summary>
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
