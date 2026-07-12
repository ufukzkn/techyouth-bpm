using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public class AuthorizationAndWorkflowIntegrationTests
{
    [Fact]
    public async Task Normal_User_Cannot_Create_Managed_User()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "user", "user123");
        var username = $"forbidden-{Guid.NewGuid():N}";
        using var request = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/users", session.Token);
        request.Content = JsonContent.Create(new CreateUserRequest(
            username,
            "Forbidden User",
            $"{username}@test.local",
            Role.User,
            UserStatus.Active,
            "TempPass123!"));

        using var response = await client.SendAsync(request);
        var exists = await factory.ExecuteDbAsync(db => db.Users.AnyAsync(user => user.Username == username));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(exists);
    }

    [Fact]
    public async Task Community_Admin_Cannot_Create_User_In_Another_Community()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "fatih.terim", "imparator123");
        var targetCommunityId = await factory.ExecuteDbAsync(db => db.Communities
            .Where(community => community.Name == "Lojistik")
            .Select(community => community.Id)
            .SingleAsync());
        var username = $"cross-scope-{Guid.NewGuid():N}";
        using var request = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/communities/{targetCommunityId}/users",
            session.Token);
        request.Content = JsonContent.Create(new CreateUserRequest(
            username,
            "Cross Scope User",
            $"{username}@test.local",
            Role.User,
            UserStatus.Active,
            "TempPass123!",
            targetCommunityId));

        using var response = await client.SendAsync(request);
        var exists = await factory.ExecuteDbAsync(db => db.Users.AnyAsync(user => user.Username == username));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(exists);
    }

    [Fact]
    public async Task SuperAdmin_Can_Read_Global_User_And_Community_Management()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client);

        using var usersRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Get, "/api/users?page=1&pageSize=5", session.Token);
        using var usersResponse = await client.SendAsync(usersRequest);
        using var communitiesRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Get, "/api/communities", session.Token);
        using var communitiesResponse = await client.SendAsync(communitiesRequest);

        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, communitiesResponse.StatusCode);
    }

    [Fact]
    public async Task Swagger_Document_Exposes_Bearer_Security_Scheme()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task SuperAdmin_Can_Create_Form_And_Start_Process_Through_Http_Api()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client);
        var communityId = await factory.ExecuteDbAsync(db => db.Communities
            .Where(community => community.IsActive)
            .Select(community => community.Id)
            .FirstAsync());

        using var formRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/forms", session.Token);
        formRequest.Content = JsonContent.Create(new
        {
            name = "HTTP Integration Form",
            description = "Created through the real controller pipeline.",
            communityId,
            fields = new[]
            {
                new
                {
                    key = "requestTitle",
                    label = "Request title",
                    type = "Text",
                    required = true,
                    sortOrder = 1,
                    options = Array.Empty<string>(),
                    validationRules = Array.Empty<object>()
                }
            }
        });
        using var formResponse = await client.SendAsync(formRequest);
        var formPayload = await formResponse.Content.ReadFromJsonAsync<JsonElement>();
        var formId = formPayload.GetProperty("id").GetGuid();

        using var processRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/processes/start", session.Token);
        processRequest.Content = JsonContent.Create(new
        {
            formDefinitionId = formId,
            formData = new { requestTitle = "Integration request" }
        });
        using var processResponse = await client.SendAsync(processRequest);
        var processPayload = await processResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, formResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, processResponse.StatusCode);
        Assert.Equal("InProgress", processPayload.GetProperty("status").GetString());
        Assert.Single(processPayload.GetProperty("tasks").EnumerateArray());
    }
}
