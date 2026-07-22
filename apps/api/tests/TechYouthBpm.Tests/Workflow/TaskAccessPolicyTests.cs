using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class TaskAccessPolicyTests
{
    private readonly TaskAccessPolicy policy = new();

    [Fact]
    public void ManageAll_Bypasses_Team_Role_And_Lead_Restrictions_Inside_Community()
    {
        var communityId = Guid.NewGuid();
        var task = CandidateTask(communityId, Guid.NewGuid(), Guid.NewGuid(), requiresTeamLead: true);
        var user = User(
            communityId,
            Guid.NewGuid(),
            [PermissionNames.TasksView, PermissionNames.TasksManageAll],
            []);

        var access = policy.Evaluate(task, user);

        Assert.True(policy.CanSee(task, user));
        Assert.True(access.CanClaim);
        Assert.False(access.CanAct);
        Assert.Null(access.ClaimDenialReasonCode);
    }

    [Fact]
    public void ManageAll_Cannot_Act_On_Task_Claimed_By_Another_User()
    {
        var communityId = Guid.NewGuid();
        var task = CandidateTask(communityId, Guid.NewGuid(), Guid.NewGuid());
        task.Status = ProcessTaskStatus.Claimed;
        task.ClaimedByUserId = Guid.NewGuid();
        var user = User(
            communityId,
            Guid.NewGuid(),
            [PermissionNames.TasksView, PermissionNames.TasksManageAll],
            []);

        var access = policy.Evaluate(task, user);

        Assert.True(policy.CanSee(task, user));
        Assert.False(access.CanAct);
        Assert.False(access.CanClaim);
        Assert.Equal(TaskActionDenialReasonCodes.ClaimedByAnotherUser, access.ActionDenialReasonCode);
    }

    [Fact]
    public void MultiTeam_Lead_Is_Evaluated_Against_The_Exact_Target_Team()
    {
        var communityId = Guid.NewGuid();
        var targetTeamId = Guid.NewGuid();
        var targetRoleId = Guid.NewGuid();
        var task = CandidateTask(communityId, targetTeamId, targetRoleId, requiresTeamLead: true);
        var user = User(
            communityId,
            targetRoleId,
            [PermissionNames.TasksView, PermissionNames.TasksAct],
            [
                new UserTeamDto(Guid.NewGuid(), "Other lead team", true),
                new UserTeamDto(Guid.NewGuid(), "Other member team", false),
                new UserTeamDto(targetTeamId, "Target team", true)
            ]);

        var access = policy.Evaluate(task, user);

        Assert.True(access.CanClaim);
        Assert.Null(access.ClaimDenialReasonCode);
    }

    [Fact]
    public void Normal_User_Must_Match_Target_Role_And_Community()
    {
        var communityId = Guid.NewGuid();
        var targetTeamId = Guid.NewGuid();
        var task = CandidateTask(communityId, targetTeamId, Guid.NewGuid());
        var wrongRole = User(
            communityId,
            Guid.NewGuid(),
            [PermissionNames.TasksView, PermissionNames.TasksAct],
            [new UserTeamDto(targetTeamId, "Target team", true)]);
        var otherCommunity = User(
            Guid.NewGuid(),
            task.CandidateCommunityRoleId,
            [PermissionNames.TasksView, PermissionNames.TasksAct, PermissionNames.TasksManageAll],
            [new UserTeamDto(targetTeamId, "Target team", true)]);

        var roleAccess = policy.Evaluate(task, wrongRole);
        var communityAccess = policy.Evaluate(task, otherCommunity);

        Assert.Equal(TaskActionDenialReasonCodes.CommunityRoleRequired, roleAccess.ClaimDenialReasonCode);
        Assert.Equal(TaskActionDenialReasonCodes.CommunityMismatch, communityAccess.ClaimDenialReasonCode);
        Assert.False(policy.CanSee(task, wrongRole));
        Assert.False(policy.CanSee(task, otherCommunity));
    }

    private static ProcessTask CandidateTask(
        Guid communityId,
        Guid teamId,
        Guid roleId,
        bool requiresTeamLead = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = Guid.NewGuid(),
            ProcessInstance = new ProcessInstance
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId
            },
            Status = ProcessTaskStatus.Open,
            AssignmentType = TaskAssignmentType.TeamAndCommunityRole,
            CandidateTeamId = teamId,
            CandidateCommunityRoleId = roleId,
            RequiresTeamLead = requiresTeamLead,
            RequiredPermission = PermissionNames.TasksAct
        };

    private static UserDto User(
        Guid communityId,
        Guid? roleId,
        IReadOnlyList<string> permissions,
        IReadOnlyList<UserTeamDto> teams) =>
        new(
            Guid.NewGuid(),
            $"user-{Guid.NewGuid():N}",
            "Task User",
            "task-user@test.local",
            Role.User,
            UserStatus.Active,
            true,
            CommunityId: communityId,
            CommunityName: "Test Community",
            CommunityRoleId: roleId,
            CommunityRoleName: "Test Role",
            Permissions: permissions,
            Teams: teams);
}
