using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Tests.Integration;

internal sealed class ApiWebApplicationFactory(
    int rateLimitPermitLimit = 100,
    string databaseProvider = "Sqlite",
    string? connectionString = null) : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"techyouth-bpm-integration-{Guid.NewGuid():N}.db");

    private string DatabaseConnectionString => connectionString ?? $"Data Source={databasePath}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = databaseProvider,
                ["ConnectionStrings:DefaultConnection"] = DatabaseConnectionString,
                ["Seed:MockData"] = "true",
                ["Email:Provider"] = "Demo",
                ["Auth:RateLimitPermitLimit"] = rateLimitPermitLimit.ToString(),
                ["Auth:RateLimitWindowMinutes"] = "1"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                if (databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
                    || databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseNpgsql(DatabaseConnectionString);
                }
                else
                {
                    options.UseSqlite(DatabaseConnectionString);
                }
            });
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    public async Task ExecuteDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    public async Task<T> ExecuteDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (connectionString is null && File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}
