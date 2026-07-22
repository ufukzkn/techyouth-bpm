using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public sealed class SportifQuickWorkflowIntegrationTests
{
    [Fact]
    public async Task Three_sportif_demo_workflows_complete_both_http_outcomes_with_their_real_candidates()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var starter = await IntegrationTestHttp.LoginAsync(client, "sport.starter", "sport123");
        var actors = new Dictionary<string, string>
        {
            ["sport.scout"] = (await IntegrationTestHttp.LoginAsync(client, "sport.scout", "sport123")).Session.Token,
            ["sport.approver"] = (await IntegrationTestHttp.LoginAsync(client, "sport.approver", "sport123")).Session.Token,
            ["sport.finance"] = (await IntegrationTestHttp.LoginAsync(client, "sport.finance", "sport123")).Session.Token,
        };
        var versions = await factory.ExecuteDbAsync(db => db.ProcessDefinitionVersions
            .Where(version => version.Status == DefinitionVersionStatus.Published
                && version.ProcessDefinition != null
                && (version.ProcessDefinition.Name == "Hizli Scout Incelemesi"
                    || version.ProcessDefinition.Name == "Hizli Teknik Onay"
                    || version.ProcessDefinition.Name == "Hizli Lider Onayi"))
            .Select(version => new { Name = version.ProcessDefinition!.Name, version.Id })
            .ToDictionaryAsync(version => version.Name, version => version.Id));
        var scenarios = new[]
        {
            new Scenario("Hizli Scout Incelemesi", "sport.scout", "oyuncuAdi", "Demo Oyuncu", "scoutNotu"),
            new Scenario("Hizli Teknik Onay", "sport.approver", "talepBasligi", "Teknik Talep", "teknikNot"),
            new Scenario("Hizli Lider Onayi", "sport.finance", "butceKalemi", "Transfer Butcesi", "maliNot"),
        };

        foreach (var scenario in scenarios)
        {
            foreach (var action in new[] { "Approve", "Reject" })
            {
                using var startRequest = IntegrationTestHttp.BearerRequest(
                    HttpMethod.Post,
                    "/api/processes/start/version",
                    starter.Session.Token);
                startRequest.Content = JsonContent.Create(new
                {
                    processDefinitionVersionId = versions[scenario.WorkflowName],
                    formData = new Dictionary<string, object> { [scenario.StartField] = scenario.StartValue },
                });
                using var startResponse = await client.SendAsync(startRequest);
                Assert.True(startResponse.IsSuccessStatusCode, await startResponse.Content.ReadAsStringAsync());
                var started = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
                var taskId = started.GetProperty("tasks").EnumerateArray()
                    .Single(task => task.GetProperty("status").GetString() == "Open")
                    .GetProperty("id")
                    .GetGuid();

                using var claimRequest = IntegrationTestHttp.BearerRequest(
                    HttpMethod.Post,
                    $"/api/tasks/{taskId}/claim",
                    actors[scenario.ActorUsername]);
                claimRequest.Content = JsonContent.Create(new { });
                using var claimResponse = await client.SendAsync(claimRequest);
                Assert.True(claimResponse.IsSuccessStatusCode, await claimResponse.Content.ReadAsStringAsync());

                using var actionRequest = IntegrationTestHttp.BearerRequest(
                    HttpMethod.Post,
                    $"/api/tasks/{taskId}/actions",
                    actors[scenario.ActorUsername]);
                actionRequest.Content = JsonContent.Create(new
                {
                    action,
                    note = $"HTTP demo {action}",
                    formData = new Dictionary<string, object> { [scenario.TaskField] = $"{action} notu" },
                });
                using var actionResponse = await client.SendAsync(actionRequest);
                Assert.True(actionResponse.IsSuccessStatusCode, await actionResponse.Content.ReadAsStringAsync());
                var completed = await actionResponse.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(action == "Approve" ? "Completed" : "Rejected", completed.GetProperty("status").GetString());
            }
        }
    }

    private sealed record Scenario(
        string WorkflowName,
        string ActorUsername,
        string StartField,
        string StartValue,
        string TaskField);
}
