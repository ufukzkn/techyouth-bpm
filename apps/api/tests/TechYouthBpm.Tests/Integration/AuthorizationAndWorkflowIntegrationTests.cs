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

    [Fact]
    public async Task Published_Dynamic_Workflow_Runs_Through_The_Http_Api()
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
            name = $"Dynamic HTTP Form {Guid.NewGuid():N}",
            description = "Starts a version-pinned workflow.",
            communityId,
            fields = new[]
            {
                new
                {
                    key = "amount",
                    label = "Amount",
                    type = "Number",
                    required = true,
                    sortOrder = 1,
                    options = Array.Empty<string>(),
                    validationRules = Array.Empty<object>()
                }
            }
        });
        using var formResponse = await client.SendAsync(formRequest);
        Assert.True(formResponse.IsSuccessStatusCode, await formResponse.Content.ReadAsStringAsync());
        var form = await formResponse.Content.ReadFromJsonAsync<JsonElement>();
        var formVersionId = form.GetProperty("latestPublishedVersionId").GetGuid();

        using var definitionRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/process-definitions", session.Token);
        definitionRequest.Content = JsonContent.Create(new
        {
            name = $"Dynamic HTTP Workflow {Guid.NewGuid():N}",
            description = "Controller pipeline test.",
            communityId
        });
        using var definitionResponse = await client.SendAsync(definitionRequest);
        Assert.True(definitionResponse.IsSuccessStatusCode, await definitionResponse.Content.ReadAsStringAsync());
        var definition = await definitionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var definitionId = definition.GetProperty("id").GetGuid();
        var graph = new
        {
            schemaVersion = "1.0",
            nodes = new object[]
            {
                new { key = "start", type = "Start", title = "Start", formDefinitionVersionId = formVersionId },
                new
                {
                    key = "operation",
                    type = "UserTask",
                    title = "Operation",
                    actions = new[] { "Complete" },
                    assignment = new { type = "ProcessStarter" }
                },
                new { key = "completed", type = "CompletedEnd", title = "Completed" }
            },
            edges = new object[]
            {
                new { source = "start", target = "operation" },
                new { source = "operation", target = "completed", action = "Complete" }
            }
        };

        using var versionRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/process-definitions/{definitionId}/versions",
            session.Token);
        versionRequest.Content = JsonContent.Create(new { formDefinitionVersionId = formVersionId, graph });
        using var versionResponse = await client.SendAsync(versionRequest);
        Assert.True(versionResponse.IsSuccessStatusCode, await versionResponse.Content.ReadAsStringAsync());
        var version = await versionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var versionId = version.GetProperty("id").GetGuid();

        using var publishRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/process-definitions/{definitionId}/versions/{versionId}/publish",
            session.Token);
        using var publishResponse = await client.SendAsync(publishRequest);
        Assert.True(publishResponse.IsSuccessStatusCode, await publishResponse.Content.ReadAsStringAsync());
        using var runnableRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/process-definitions/runnable",
            session.Token);
        using var runnableResponse = await client.SendAsync(runnableRequest);
        var runnable = await runnableResponse.Content.ReadFromJsonAsync<JsonElement>();

        using var startRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/processes/start/version", session.Token);
        startRequest.Content = JsonContent.Create(new
        {
            processDefinitionVersionId = versionId,
            formData = new { amount = 125000 }
        });
        using var startResponse = await client.SendAsync(startRequest);
        Assert.True(startResponse.IsSuccessStatusCode, await startResponse.Content.ReadAsStringAsync());
        var process = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = process.GetProperty("tasks").EnumerateArray().Single().GetProperty("id").GetGuid();

        using var actionRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/tasks/{taskId}/actions",
            session.Token);
        actionRequest.Content = JsonContent.Create(new { action = "Complete", note = "HTTP operation completed." });
        using var actionResponse = await client.SendAsync(actionRequest);
        var completed = await actionResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, formResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, definitionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Contains(runnable.EnumerateArray(), item =>
            item.GetProperty("processDefinitionVersionId").GetGuid() == versionId);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.Equal("InProgress", process.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, actionResponse.StatusCode);
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
    }
}
