using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public class WorkflowVisibilityIntegrationTests
{
    [Fact]
    public async Task Normal_User_Cannot_Request_Community_Or_Global_Workflow_Scope()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "user", "user123");

        using var dashboardRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/dashboard/summary?scope=community",
            session.Token);
        using var dashboardResponse = await client.SendAsync(dashboardRequest);
        using var processRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/processes?scope=global",
            session.Token);
        using var processResponse = await client.SendAsync(processRequest);

        Assert.Equal(HttpStatusCode.Forbidden, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, processResponse.StatusCode);
    }

    [Fact]
    public async Task Okan_Has_Empty_Personal_Workflow_Scope_Until_He_Starts_A_Process()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "okan.buruk", "okan123");

        var dashboard = await GetAsync(client, "/api/dashboard/summary?scope=personal", session.Token);
        var processes = await GetAsync(client, "/api/processes?scope=personal", session.Token);
        var tasks = await GetAsync(client, "/api/tasks/my", session.Token);

        Assert.Equal(0, dashboard.GetProperty("openTaskCount").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("inProgressProcessCount").GetInt32());
        Assert.Equal(0, processes.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, tasks.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Starter_Sees_Five_Seed_Processes_But_No_Unrelated_Candidate_Tasks()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "user", "user123");

        var processes = await GetAsync(client, "/api/processes?scope=personal&pageSize=20", session.Token);
        var tasks = await GetAsync(client, "/api/tasks/my", session.Token);

        Assert.Equal(5, processes.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, tasks.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Fatih_Sees_All_Community_Tasks_And_Community_Wide_Process_Summary()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "fatih.terim", "imparator123");

        var tasks = await GetAsync(client, "/api/tasks/my", session.Token);
        var processes = await GetAsync(client, "/api/processes?scope=community&pageSize=20", session.Token);
        var dashboard = await GetAsync(client, "/api/dashboard/summary?scope=community", session.Token);
        var expected = await factory.ExecuteDbAsync(async db =>
        {
            var communityId = await db.UserCommunityMemberships
                .Where(membership => membership.User != null && membership.User.Username == "fatih.terim" && membership.IsActive)
                .Select(membership => membership.CommunityId)
                .SingleAsync();
            var taskCount = await db.ProcessTasks.CountAsync(task =>
                task.ProcessInstance != null
                && task.ProcessInstance.CommunityId == communityId
                && (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed));
            var processCount = await db.ProcessInstances.CountAsync(process => process.CommunityId == communityId);
            return (taskCount, processCount);
        });

        Assert.Equal(expected.taskCount, tasks.GetProperty("totalCount").GetInt32());
        Assert.Contains(tasks.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("title").GetString() == "Transfer Operasyon Onayi");
        Assert.Equal(expected.processCount, processes.GetProperty("totalCount").GetInt32());
        Assert.Equal(expected.taskCount, dashboard.GetProperty("openTaskCount").GetInt32());
    }

    [Theory]
    [InlineData("approver", "approver123")]
    [InlineData("quaresma", "trivela123")]
    public async Task Technical_Approvers_See_The_Two_Tasks_In_Their_Candidate_Pool(
        string username,
        string password)
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, username, password);

        var tasks = await GetAsync(client, "/api/tasks/my?pageSize=10", session.Token);
        var items = tasks.GetProperty("items").EnumerateArray().ToArray();

        Assert.True(tasks.GetProperty("totalCount").GetInt32() >= 2);
        Assert.True(items.Count(item => item.GetProperty("title").GetString() == "Teknik Degerlendirme") >= 2);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string path, string token)
    {
        using var request = IntegrationTestHttp.BearerRequest(HttpMethod.Get, path, token);
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}
