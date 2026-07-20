using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TechYouthBpm.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Sqlite";
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Data Source=techyouth-bpm.db";
        var databaseSchema = Environment.GetEnvironmentVariable("Database__Schema");

        var options = new DbContextOptionsBuilder<AppDbContext>();
        switch (provider.Trim().ToLowerInvariant())
        {
            case "postgresql":
            case "postgres":
                options.UseNpgsql(connectionString, providerOptions =>
                {
                    if (!string.IsNullOrWhiteSpace(databaseSchema))
                    {
                        providerOptions.MigrationsHistoryTable(
                            HistoryRepository.DefaultTableName,
                            databaseSchema);
                    }
                });
                break;
            case "sqlite":
                options.UseSqlite(connectionString);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported database provider '{provider}'. Use 'Sqlite' or 'PostgreSql'.");
        }

        return new AppDbContext(options.Options);
    }
}
