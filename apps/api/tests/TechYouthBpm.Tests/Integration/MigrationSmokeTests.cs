using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TechYouthBpm.Tests.Integration;

public class MigrationSmokeTests
{
    [Fact]
    public async Task Sqlite_Startup_Applies_All_Migrations_And_Seed_Data()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        var snapshot = await factory.ExecuteDbAsync(async db => new
        {
            PendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray(),
            AppliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray(),
            CommunityCount = await db.Communities.CountAsync(),
            HasSuperAdmin = await db.Users.AnyAsync(user => user.Username == "admin")
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(snapshot.PendingMigrations);
        Assert.NotEmpty(snapshot.AppliedMigrations);
        Assert.Equal(5, snapshot.CommunityCount);
        Assert.True(snapshot.HasSuperAdmin);
    }

    [Fact]
    public async Task PostgreSql_Startup_Applies_Migrations_And_Serves_Auth_And_Forms_When_Configured()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("TECHYOUTH_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return;
        }

        var schemaName = $"techyouth_smoke_{Guid.NewGuid():N}";
        await CreateSchemaAsync(baseConnectionString, schemaName);

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = schemaName
            };
            using var factory = new ApiWebApplicationFactory(
                databaseProvider: "PostgreSql",
                connectionString: builder.ConnectionString);
            using var client = factory.CreateApiClient();

            var (session, _) = await IntegrationTestHttp.LoginAsync(client);
            using var formsRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Get, "/api/forms", session.Token);
            using var formsResponse = await client.SendAsync(formsRequest);
            var lifecycle = await FormLifecycleHttpScenario.RunAsync(
                client,
                session.Token,
                "PostgreSQL migration lifecycle");
            var pendingMigrations = await factory.ExecuteDbAsync(async db =>
                (await db.Database.GetPendingMigrationsAsync()).ToArray());

            Assert.Equal(HttpStatusCode.OK, formsResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, lifecycle.FormUpdateStatus);
            Assert.Equal(HttpStatusCode.OK, lifecycle.UpdateStatus);
            Assert.Equal(HttpStatusCode.OK, lifecycle.PublishDraftUpdateStatus);
            Assert.Equal("Published", lifecycle.PublishedStatus);
            Assert.Equal("InProgress", lifecycle.ProcessStatus);
            Assert.Equal("Archived", lifecycle.ArchivedStatus);
            Assert.Empty(pendingMigrations);
        }
        finally
        {
            await DropSchemaAsync(baseConnectionString, schemaName);
        }
    }

    private static async Task CreateSchemaAsync(string connectionString, string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropSchemaAsync(string connectionString, string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }
}
