using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public class TaskManageAllIntegrationTests
{
    [Fact]
    public async Task Same_Token_Uses_The_Latest_ManageAll_Permission()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "fatih.terim", "imparator123");
        var fixture = await SeedCandidateTaskAsync(factory, "fatih.terim", requiresTeamLead: true);

        var before = await GetTaskCountAsync(client, session.Token, fixture.TaskId);
        await factory.ExecuteDbAsync(async db =>
        {
            var permission = await db.CommunityRolePermissions.SingleAsync(item =>
                item.CommunityRoleId == fixture.ActorRoleId
                && item.Permission == PermissionNames.TasksManageAll);
            db.CommunityRolePermissions.Remove(permission);
            await db.SaveChangesAsync();
        });
        var after = await GetTaskCountAsync(client, session.Token, fixture.TaskId);

        Assert.Equal(1, before);
        Assert.Equal(0, after);
    }

    [Fact]
    public async Task Same_Token_Uses_New_Exact_Target_Team_Lead_Membership()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "approver", "approver123");
        var fixture = await SeedCandidateTaskAsync(factory, "approver", requiresTeamLead: true);
        await SeedOtherLeadMembershipsAsync(factory, fixture.ActorUserId, fixture.CommunityId);

        var before = await GetTaskCountAsync(client, session.Token, fixture.TaskId);
        await factory.ExecuteDbAsync(async db =>
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                Id = Guid.NewGuid(),
                TeamId = fixture.TargetTeamId,
                UserId = fixture.ActorUserId,
                IsLead = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
        var after = await GetTaskCountAsync(client, session.Token, fixture.TaskId);

        using var claimRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/tasks/{fixture.TaskId}/claim",
            session.Token);
        claimRequest.Content = JsonContent.Create(new { claimVersion = fixture.ClaimVersion });
        using var claimResponse = await client.SendAsync(claimRequest);

        Assert.Equal(0, before);
        Assert.Equal(1, after);
        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
    }

    [Fact]
    public async Task ManageAll_Sees_But_Cannot_Act_On_Task_Claimed_By_Another_User()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "fatih.terim", "imparator123");
        var fixture = await SeedCandidateTaskAsync(factory, "fatih.terim", requiresTeamLead: false, claimedBy: "approver");

        var visibleCount = await GetTaskCountAsync(client, session.Token, fixture.TaskId);
        using var actionRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/tasks/{fixture.TaskId}/actions",
            session.Token);
        actionRequest.Content = JsonContent.Create(new { action = "Approve", note = "Must remain blocked." });
        using var actionResponse = await client.SendAsync(actionRequest);
        var status = await factory.ExecuteDbAsync(db => db.ProcessTasks
            .Where(task => task.Id == fixture.TaskId)
            .Select(task => task.Status)
            .SingleAsync());

        Assert.Equal(1, visibleCount);
        Assert.Equal(HttpStatusCode.BadRequest, actionResponse.StatusCode);
        Assert.Equal(ProcessTaskStatus.Claimed, status);
    }

    [Fact]
    public async Task ManageAll_Does_Not_Expose_Another_Community_Task()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, "fatih.terim", "imparator123");
        var otherTaskId = await factory.ExecuteDbAsync(db => db.ProcessTasks
            .Where(task => task.ProcessInstance != null
                && task.ProcessInstance.Community != null
                && task.ProcessInstance.Community.Name == "Lojistik")
            .Select(task => task.Id)
            .FirstAsync());

        var count = await GetTaskCountAsync(client, session.Token, otherTaskId);

        Assert.Equal(0, count);
    }

    private static async Task<int> GetTaskCountAsync(HttpClient client, string token, Guid taskId)
    {
        using var request = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            $"/api/tasks/my?view=active&taskId={taskId}",
            token);
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        var payload = JsonSerializer.Deserialize<JsonElement>(content);
        return payload.GetProperty("totalCount").GetInt32();
    }

    private static Task SeedOtherLeadMembershipsAsync(
        ApiWebApplicationFactory factory,
        Guid userId,
        Guid communityId) =>
        factory.ExecuteDbAsync(async db =>
        {
            foreach (var name in new[] { "Other Lead A", "Other Lead B" })
            {
                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    CommunityId = communityId,
                    Name = $"{name} {Guid.NewGuid():N}",
                    NormalizedName = $"{name}-{Guid.NewGuid():N}".ToUpperInvariant(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Teams.Add(team);
                db.TeamMemberships.Add(new TeamMembership
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    UserId = userId,
                    IsLead = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        });

    private static Task<TaskFixture> SeedCandidateTaskAsync(
        ApiWebApplicationFactory factory,
        string actorUsername,
        bool requiresTeamLead,
        string? claimedBy = null) =>
        factory.ExecuteDbAsync(async db =>
        {
            var actor = await db.Users
                .Include(user => user.CommunityMemberships.Where(membership => membership.IsActive))
                .SingleAsync(user => user.Username == actorUsername);
            var membership = Assert.Single(actor.CommunityMemberships);
            var targetRoleId = actorUsername == "approver"
                ? membership.CommunityRoleId
                : await db.CommunityRoles
                    .Where(role => role.CommunityId == membership.CommunityId
                        && role.Id != membership.CommunityRoleId
                        && role.Permissions.Any(permission => permission.Permission == PermissionNames.TasksAct))
                    .Select(role => role.Id)
                    .FirstAsync();
            var team = new Team
            {
                Id = Guid.NewGuid(),
                CommunityId = membership.CommunityId,
                Name = $"Target Team {Guid.NewGuid():N}",
                NormalizedName = $"TARGET-{Guid.NewGuid():N}".ToUpperInvariant(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Teams.Add(team);
            var processId = await db.ProcessInstances
                .Where(process => process.CommunityId == membership.CommunityId)
                .Select(process => process.Id)
                .FirstAsync();
            Guid? claimantId = null;
            if (claimedBy is not null)
            {
                claimantId = await db.Users
                    .Where(user => user.Username == claimedBy)
                    .Select(user => user.Id)
                    .SingleAsync();
            }

            var task = new ProcessTask
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = processId,
                NodeKey = $"security-{Guid.NewGuid():N}",
                Title = "Security policy task",
                RequiredPermission = PermissionNames.TasksAct,
                Status = claimantId.HasValue ? ProcessTaskStatus.Claimed : ProcessTaskStatus.Open,
                AssignmentType = TaskAssignmentType.TeamAndCommunityRole,
                CandidateTeamId = team.Id,
                CandidateCommunityRoleId = targetRoleId,
                RequiresTeamLead = requiresTeamLead,
                ClaimedByUserId = claimantId,
                ClaimedAt = claimantId.HasValue ? DateTime.UtcNow : null,
                ClaimVersion = Guid.NewGuid(),
                AvailableActionsJson = "[2,3]",
                CreatedAt = DateTime.UtcNow
            };
            db.ProcessTasks.Add(task);
            await db.SaveChangesAsync();
            return new TaskFixture(
                task.Id,
                task.ClaimVersion,
                actor.Id,
                membership.CommunityRoleId,
                membership.CommunityId,
                team.Id);
        });

    private sealed record TaskFixture(
        Guid TaskId,
        Guid ClaimVersion,
        Guid ActorUserId,
        Guid ActorRoleId,
        Guid CommunityId,
        Guid TargetTeamId);
}
