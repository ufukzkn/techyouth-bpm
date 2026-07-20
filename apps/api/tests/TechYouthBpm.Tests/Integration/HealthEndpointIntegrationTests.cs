using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public sealed class HealthEndpointIntegrationTests
{
    [Fact]
    public async Task Live_And_Ready_Endpoints_Return_Safe_Healthy_Reports()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();

        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");
        var live = await liveResponse.Content.ReadFromJsonAsync<JsonElement>();
        var readyRaw = await readyResponse.Content.ReadAsStringAsync();
        var ready = JsonSerializer.Deserialize<JsonElement>(readyRaw);

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal("Healthy", live.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal("Healthy", ready.GetProperty("status").GetString());
        Assert.Contains(ready.GetProperty("checks").EnumerateArray(), check =>
            check.GetProperty("name").GetString() == "database"
            && check.GetProperty("status").GetString() == "Healthy");
        Assert.DoesNotContain("Data Source", readyRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", readyRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", readyRaw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ready_Returns_ServiceUnavailable_When_SuperAdmin_Invariant_Is_Broken()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        await factory.ExecuteDbAsync(async db =>
        {
            var superAdmin = await db.Users.SingleAsync(user => user.Role == Role.SuperAdmin);
            superAdmin.Role = Role.User;
            await db.SaveChangesAsync();
        });

        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Contains(body.GetProperty("checks").EnumerateArray(), check =>
            check.GetProperty("name").GetString() == "superadmin"
            && check.GetProperty("status").GetString() == "Unhealthy");
    }
}
