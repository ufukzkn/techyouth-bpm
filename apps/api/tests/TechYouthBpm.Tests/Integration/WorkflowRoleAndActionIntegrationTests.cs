using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public class WorkflowRoleAndActionIntegrationTests
{
    [Fact]
    public async Task Transfer_Workflow_Enforces_Form_Team_Role_Claim_Approve_And_Reject_Through_Http()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (starter, _) = await IntegrationTestHttp.LoginAsync(client, "user", "user123");
        var (wrongRole, _) = await IntegrationTestHttp.LoginAsync(client, "user", "user123");
        var (scoutReviewer, _) = await IntegrationTestHttp.LoginAsync(client, "quaresma", "trivela123");
        var (technicalReviewer, _) = await IntegrationTestHttp.LoginAsync(client, "approver", "approver123");

        var workflow = await factory.ExecuteDbAsync(db => db.ProcessDefinitionVersions
            .Where(version => version.Status == DefinitionVersionStatus.Published
                && version.ProcessDefinition != null
                && version.ProcessDefinition.Name == "Transfer Teklif ve Onay Akışı")
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new { version.Id, version.ProcessDefinitionId })
            .FirstAsync());
        var processCountBefore = await factory.ExecuteDbAsync(db => db.ProcessInstances
            .CountAsync(process => process.ProcessDefinitionVersionId == workflow.Id));

        using (var invalidStart = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   "/api/processes/start/version",
                   starter.Token))
        {
            invalidStart.Content = JsonContent.Create(new
            {
                processDefinitionVersionId = workflow.Id,
                formData = TransferStartFormData(gerekce: string.Empty)
            });
            using var invalidResponse = await client.SendAsync(invalidStart);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }

        var processCountAfterInvalidStart = await factory.ExecuteDbAsync(db => db.ProcessInstances
            .CountAsync(process => process.ProcessDefinitionVersionId == workflow.Id));
        Assert.Equal(processCountBefore, processCountAfterInvalidStart);

        JsonElement startedProcess;
        using (var validStart = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   "/api/processes/start/version",
                   starter.Token))
        {
            validStart.Content = JsonContent.Create(new
            {
                processDefinitionVersionId = workflow.Id,
                formData = TransferStartFormData("Acil teknik ve mali değerlendirme gerekiyor.")
            });
            using var startResponse = await client.SendAsync(validStart);
            Assert.True(startResponse.IsSuccessStatusCode, await startResponse.Content.ReadAsStringAsync());
            startedProcess = await ReadJsonAsync(startResponse);
        }

        var processId = startedProcess.GetProperty("id").GetGuid();
        var scoutTask = startedProcess.GetProperty("tasks").EnumerateArray()
            .Single(task => task.GetProperty("nodeKey").GetString() == "scoutReview");
        var scoutTaskId = scoutTask.GetProperty("id").GetGuid();

        var wrongRoleTasks = await GetTasksAsync(client, wrongRole.Token, scoutTaskId);
        Assert.Equal(0, wrongRoleTasks.GetProperty("totalCount").GetInt32());
        using (var forbiddenClaim = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   $"/api/tasks/{scoutTaskId}/claim",
                   wrongRole.Token))
        {
            forbiddenClaim.Content = JsonContent.Create(new { });
            using var forbiddenResponse = await client.SendAsync(forbiddenClaim);
            Assert.Equal(HttpStatusCode.BadRequest, forbiddenResponse.StatusCode);
        }

        var candidateTasks = await GetTasksAsync(client, scoutReviewer.Token, scoutTaskId);
        Assert.Equal(1, candidateTasks.GetProperty("totalCount").GetInt32());
        var candidateTask = candidateTasks.GetProperty("items").EnumerateArray().Single();
        Assert.True(!candidateTask.TryGetProperty("taskForm", out var summaryTaskForm)
            || summaryTaskForm.ValueKind == JsonValueKind.Null);

        using (var taskDetailRequest = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Get,
                   $"/api/tasks/{scoutTaskId}",
                   scoutReviewer.Token))
        {
            using var taskDetailResponse = await client.SendAsync(taskDetailRequest);
            Assert.True(taskDetailResponse.IsSuccessStatusCode, await taskDetailResponse.Content.ReadAsStringAsync());
            var taskDetail = await ReadJsonAsync(taskDetailResponse);
            Assert.True(taskDetail.GetProperty("taskForm").GetProperty("pages").GetArrayLength() > 0);
        }

        using (var hiddenTaskDetailRequest = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Get,
                   $"/api/tasks/{scoutTaskId}",
                   wrongRole.Token))
        {
            using var hiddenTaskDetailResponse = await client.SendAsync(hiddenTaskDetailRequest);
            Assert.Equal(HttpStatusCode.NotFound, hiddenTaskDetailResponse.StatusCode);
        }

        await ClaimAsync(client, scoutReviewer.Token, scoutTaskId);
        using (var invalidTaskForm = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   $"/api/tasks/{scoutTaskId}/actions",
                   scoutReviewer.Token))
        {
            invalidTaskForm.Content = JsonContent.Create(new
            {
                action = "Approve",
                note = "Eksik task formu ile ilerlememeli.",
                formData = new { raporOzeti = "Eksik veri" }
            });
            using var invalidTaskFormResponse = await client.SendAsync(invalidTaskForm);
            Assert.Equal(HttpStatusCode.BadRequest, invalidTaskFormResponse.StatusCode);
        }
        await ReleaseAsync(client, scoutReviewer.Token, scoutTaskId);
        await ClaimAsync(client, scoutReviewer.Token, scoutTaskId);

        JsonElement afterScoutApproval;
        using (var approveRequest = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   $"/api/tasks/{scoutTaskId}/actions",
                   scoutReviewer.Token))
        {
            approveRequest.Content = JsonContent.Create(new
            {
                action = "Approve",
                note = "Scout incelemesi olumlu tamamlandı.",
                formData = new
                {
                    raporOzeti = "Oyuncunun son maçları ve sağlık durumu incelendi.",
                    scoutTavsiyesi = "Olumlu",
                    izlemePuani = 9
                }
            });
            using var approveResponse = await client.SendAsync(approveRequest);
            Assert.True(approveResponse.IsSuccessStatusCode, await approveResponse.Content.ReadAsStringAsync());
            afterScoutApproval = await ReadJsonAsync(approveResponse);
        }

        var technicalTask = afterScoutApproval.GetProperty("tasks").EnumerateArray()
            .Single(task => task.GetProperty("nodeKey").GetString() == "technicalReview"
                && task.GetProperty("status").GetString() == "Open");
        var technicalTaskId = technicalTask.GetProperty("id").GetGuid();
        await ClaimAsync(client, technicalReviewer.Token, technicalTaskId);

        using (var competingAction = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   $"/api/tasks/{technicalTaskId}/actions",
                   scoutReviewer.Token))
        {
            competingAction.Content = JsonContent.Create(new
            {
                action = "Approve",
                note = "Başka kullanıcının claim ettiği göreve müdahale edilmemeli.",
                formData = new
                {
                    teknikKarar = "Uygun",
                    teknikNot = "Bu aksiyon reddedilmelidir."
                }
            });
            using var competingActionResponse = await client.SendAsync(competingAction);
            Assert.Equal(HttpStatusCode.BadRequest, competingActionResponse.StatusCode);
        }

        JsonElement rejectedProcess;
        using (var rejectRequest = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   $"/api/tasks/{technicalTaskId}/actions",
                   technicalReviewer.Token))
        {
            rejectRequest.Content = JsonContent.Create(new
            {
                action = "Reject",
                note = "Kadro planlamasına uygun bulunmadı.",
                formData = new
                {
                    teknikKarar = "Uygun Değil",
                    teknikNot = "Mevcut kadro ve bütçe planına uygun değil."
                }
            });
            using var rejectResponse = await client.SendAsync(rejectRequest);
            Assert.True(rejectResponse.IsSuccessStatusCode, await rejectResponse.Content.ReadAsStringAsync());
            rejectedProcess = await ReadJsonAsync(rejectResponse);
        }

        Assert.Equal("Rejected", rejectedProcess.GetProperty("status").GetString());
        Assert.Equal("Rejected", rejectedProcess.GetProperty("auditLogs").EnumerateArray().Last()
            .GetProperty("toStatus").GetString());

        var persisted = await factory.ExecuteDbAsync(async db =>
        {
            var process = await db.ProcessInstances
                .AsSplitQuery()
                .Include(item => item.AuditLogs)
                .Include(item => item.StepExecutions)
                .SingleAsync(item => item.Id == processId);
            var auditActions = await db.SystemAuditLogs
                .Where(log => log.EntityId == processId.ToString()
                    || log.EntityId == scoutTaskId.ToString()
                    || log.EntityId == technicalTaskId.ToString())
                .Select(log => log.Action)
                .ToListAsync();
            var notifications = await db.Notifications
                .Where(notification => notification.EntityId == processId.ToString()
                    || notification.EntityId == scoutTaskId.ToString()
                    || notification.EntityId == technicalTaskId.ToString())
                .Select(notification => notification.Type)
                .ToListAsync();
            return new
            {
                process.Status,
                ProcessAuditActions = process.AuditLogs.Select(log => log.Action).ToArray(),
                CompletedStepOutputs = process.StepExecutions
                    .Where(step => step.Status == ProcessStepStatus.Completed)
                    .Select(step => step.OutputJson)
                    .ToArray(),
                SystemAuditActions = auditActions,
                NotificationTypes = notifications
            };
        });

        Assert.Equal(ProcessStatus.Rejected, persisted.Status);
        Assert.Contains(WorkflowAction.Start, persisted.ProcessAuditActions);
        Assert.Contains(WorkflowAction.Approve, persisted.ProcessAuditActions);
        Assert.Contains(WorkflowAction.Reject, persisted.ProcessAuditActions);
        Assert.Contains(persisted.CompletedStepOutputs, output => output.Contains("scoutTavsiyesi", StringComparison.Ordinal));
        Assert.Contains(persisted.CompletedStepOutputs, output => output.Contains("teknikKarar", StringComparison.Ordinal));
        Assert.Contains("Process.Started", persisted.SystemAuditActions);
        Assert.Contains("Task.Claimed", persisted.SystemAuditActions);
        Assert.Contains("Task.Released", persisted.SystemAuditActions);
        Assert.Contains("Task.Approve", persisted.SystemAuditActions);
        Assert.Contains("Task.Reject", persisted.SystemAuditActions);
        Assert.Contains("Task.Assigned", persisted.NotificationTypes);
        Assert.Contains("Process.Advanced", persisted.NotificationTypes);
        Assert.Contains("Process.Rejected", persisted.NotificationTypes);
    }

    [Fact]
    public async Task Existing_Session_Reevaluates_Role_And_Team_Membership_On_Every_Request()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "user", "user123");

        var workflowVersionId = await factory.ExecuteDbAsync(db => db.ProcessDefinitionVersions
            .Where(version => version.Status == DefinitionVersionStatus.Published
                && version.ProcessDefinition != null
                && version.ProcessDefinition.Name == "Transfer Teklif ve Onay Akışı")
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.Id)
            .FirstAsync());

        JsonElement startedProcess;
        using (var request = IntegrationTestHttp.BearerRequest(
                   HttpMethod.Post,
                   "/api/processes/start/version",
                   session.Token))
        {
            request.Content = JsonContent.Create(new
            {
                processDefinitionVersionId = workflowVersionId,
                formData = TransferStartFormData("Yetki yeniden değerlendirme testi.")
            });
            using var response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            startedProcess = await ReadJsonAsync(response);
        }

        var taskId = startedProcess.GetProperty("tasks").EnumerateArray()
            .Single(task => task.GetProperty("nodeKey").GetString() == "scoutReview")
            .GetProperty("id")
            .GetGuid();
        Assert.Equal(0, (await GetTasksAsync(client, session.Token, taskId))
            .GetProperty("totalCount").GetInt32());

        await factory.ExecuteDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(item => item.Username == "user");
            var membership = await db.UserCommunityMemberships
                .SingleAsync(item => item.UserId == user.Id && item.IsActive);
            var approverRoleId = await db.CommunityRoles
                .Where(role => role.CommunityId == membership.CommunityId && role.TemplateKey == "approver")
                .Select(role => role.Id)
                .SingleAsync();
            membership.CommunityRoleId = approverRoleId;
            await db.SaveChangesAsync();
        });

        Assert.Equal(1, (await GetTasksAsync(client, session.Token, taskId))
            .GetProperty("totalCount").GetInt32());

        await factory.ExecuteDbAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.Username == "user").Select(user => user.Id).SingleAsync();
            var scoutMembership = await db.TeamMemberships
                .Include(membership => membership.Team)
                .SingleAsync(membership => membership.UserId == userId
                    && membership.IsActive
                    && membership.Team != null
                    && membership.Team.Name == "Scout Ekibi");
            scoutMembership.IsActive = false;
            scoutMembership.IsLead = false;
            scoutMembership.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

        Assert.Equal(0, (await GetTasksAsync(client, session.Token, taskId))
            .GetProperty("totalCount").GetInt32());
    }

    private static object TransferStartFormData(string gerekce) => new
    {
        talepSahibi = "HTTP Test Kullanıcısı",
        iletisimEmail = "workflow.test@techyouth.local",
        oyuncuAdi = "Demo Oyuncu",
        kulup = "Beşiktaş",
        pozisyon = "Forvet",
        bonservis = 7_500_000,
        paraBirimi = "EUR",
        teklifTarihi = "2026-07-18",
        acilMi = true,
        gerekce,
        teklifDosyasi = new
        {
            name = "transfer-teklifi.pdf",
            size = 245_760,
            type = "application/pdf",
            lastModified = 1_752_787_200_000L
        },
        veriOnayi = true
    };

    private static async Task<JsonElement> GetTasksAsync(HttpClient client, string token, Guid taskId)
    {
        using var request = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            $"/api/tasks/my?page=1&pageSize=10&taskId={taskId}",
            token);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private static async Task ClaimAsync(HttpClient client, string token, Guid taskId)
    {
        using var request = IntegrationTestHttp.BearerRequest(HttpMethod.Post, $"/api/tasks/{taskId}/claim", token);
        request.Content = JsonContent.Create(new { });
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task ReleaseAsync(HttpClient client, string token, Guid taskId)
    {
        using var request = IntegrationTestHttp.BearerRequest(HttpMethod.Post, $"/api/tasks/{taskId}/release", token);
        request.Content = JsonContent.Create(new { });
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
