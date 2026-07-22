using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_Applies_User_Process_And_Task_Permissions()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.User, "process-starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "approver");
        var inProgressFormId = Guid.NewGuid();
        var completedFormId = Guid.NewGuid();
        db.FormDefinitions.AddRange(
            new FormDefinition
            {
                Id = inProgressFormId,
                Name = "In Progress Form",
                CommunityId = TestDbFactory.CommunityId,
                CreatedByUserId = starter.Id,
                CreatedAt = DateTime.UtcNow
            },
            new FormDefinition
            {
                Id = completedFormId,
                Name = "Completed Form",
                CommunityId = TestDbFactory.CommunityId,
                CreatedByUserId = starter.Id,
                CreatedAt = DateTime.UtcNow
            });
        var inProgressProcess = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = inProgressFormId,
            CommunityId = TestDbFactory.CommunityId,
            StartedByUserId = starter.Id,
            Status = ProcessStatus.InProgress,
            FormDataJson = "{}",
            StartedAt = DateTime.UtcNow
        };
        db.ProcessInstances.AddRange(
            inProgressProcess,
            new ProcessInstance
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = completedFormId,
                CommunityId = TestDbFactory.CommunityId,
                StartedByUserId = starter.Id,
                Status = ProcessStatus.Completed,
                FormDataJson = "{}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        db.ProcessTasks.Add(new ProcessTask
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = inProgressProcess.Id,
            AssignedRole = Role.User,
            RequiredPermission = PermissionNames.TasksAct,
            Status = ProcessTaskStatus.Open,
            AvailableActionsJson = "[]",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new DashboardService(db);

        var starterSummary = await service.GetSummaryAsync(TestDbFactory.ToDto(starter));
        var approverSummary = await service.GetSummaryAsync(TestDbFactory.ToDto(approver));

        Assert.Equal(0, starterSummary.OpenTaskCount);
        Assert.Equal(1, starterSummary.InProgressProcessCount);
        Assert.Equal(1, starterSummary.CompletedProcessCount);
        Assert.Equal(1, approverSummary.OpenTaskCount);
        Assert.Equal(1, approverSummary.InProgressProcessCount);
        Assert.Equal(0, approverSummary.CompletedProcessCount);
        Assert.Empty(starterSummary.RecentOpenTasks!);
        Assert.Single(approverSummary.RecentOpenTasks!);
        Assert.Equal(2, starterSummary.RecentProcesses!.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_Limits_And_Sorts_Recent_Items()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.User, "recent-starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "recent-approver");
        var now = DateTime.UtcNow;

        for (var index = 0; index < 6; index++)
        {
            var form = new FormDefinition
            {
                Id = Guid.NewGuid(),
                Name = $"Recent Form {index}",
                CommunityId = TestDbFactory.CommunityId,
                CreatedByUserId = starter.Id,
                CreatedAt = now.AddMinutes(index)
            };
            var process = new ProcessInstance
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = form.Id,
                CommunityId = TestDbFactory.CommunityId,
                StartedByUserId = starter.Id,
                Status = ProcessStatus.InProgress,
                FormDataJson = "{}",
                StartedAt = now.AddMinutes(index)
            };
            db.AddRange(
                form,
                process,
                new ProcessTask
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = process.Id,
                    RequiredPermission = PermissionNames.TasksAct,
                    Status = ProcessTaskStatus.Open,
                    AvailableActionsJson = "[]",
                    CreatedAt = now.AddMinutes(index)
                });
        }
        await db.SaveChangesAsync();
        var service = new DashboardService(db);

        var summary = await service.GetSummaryAsync(TestDbFactory.ToDto(approver));

        Assert.Equal(4, summary.RecentOpenTasks!.Count);
        Assert.Equal(4, summary.RecentProcesses!.Count);
        Assert.Equal(
            ["Recent Form 5", "Recent Form 4", "Recent Form 3", "Recent Form 2"],
            summary.RecentOpenTasks.Select(item => item.FormName).ToArray());
        Assert.Equal(
            ["Recent Form 5", "Recent Form 4", "Recent Form 3", "Recent Form 2"],
            summary.RecentProcesses.Select(item => item.FormName).ToArray());
    }

    [Fact]
    public async Task GetSummaryAsync_Uses_Explicit_Community_And_Global_Scopes()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.User, "scoped-starter");
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "global-superadmin");
        var otherCommunityId = Guid.NewGuid();
        var otherFormId = Guid.NewGuid();
        db.Communities.Add(new Community
        {
            Id = otherCommunityId,
            Name = "Other Community",
            InviteCode = "OTHR1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.FormDefinitions.Add(new FormDefinition
        {
            Id = otherFormId,
            Name = "Other Community Form",
            CommunityId = otherCommunityId,
            CreatedByUserId = starter.Id,
            CreatedAt = DateTime.UtcNow
        });
        db.ProcessInstances.Add(new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = otherFormId,
            CommunityId = otherCommunityId,
            StartedByUserId = starter.Id,
            Status = ProcessStatus.InProgress,
            FormDataJson = "{}",
            StartedAt = DateTime.UtcNow.AddMinutes(1)
        });
        await db.SaveChangesAsync();
        var service = new DashboardService(db);

        var scopedSummary = await service.GetSummaryAsync(
            TestDbFactory.CommunityAdminDto(starter),
            WorkflowVisibilityScope.Community);
        var personalSuperAdminSummary = await service.GetSummaryAsync(TestDbFactory.ToDto(superAdmin));
        var globalSummary = await service.GetSummaryAsync(
            TestDbFactory.ToDto(superAdmin),
            WorkflowVisibilityScope.Global);

        Assert.DoesNotContain(scopedSummary.RecentProcesses!, item => item.FormName == "Other Community Form");
        Assert.Empty(personalSuperAdminSummary.RecentProcesses!);
        Assert.Contains(globalSummary.RecentProcesses!, item => item.FormName == "Other Community Form");
    }

    [Fact]
    public async Task GetSummaryAsync_Counts_Escalated_Process_As_Ongoing()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.User, "escalated-starter");
        var form = new FormDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Escalated Form",
            CommunityId = TestDbFactory.CommunityId,
            CreatedByUserId = starter.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.AddRange(
            form,
            new ProcessInstance
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = form.Id,
                CommunityId = TestDbFactory.CommunityId,
                StartedByUserId = starter.Id,
                Status = ProcessStatus.Escalated,
                FormDataJson = "{}",
                StartedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync(TestDbFactory.ToDto(starter));

        Assert.Equal(1, summary.InProgressProcessCount);
    }

    [Fact]
    public async Task Personal_Summary_Does_Not_Count_A_Teammates_Claimed_Task_As_Available_Work()
    {
        await using var db = TestDbFactory.Create();
        var member = TestDbFactory.SeedUser(db, Role.Approver, "queue-member");
        var teammate = TestDbFactory.SeedUser(db, Role.Approver, "queue-teammate");
        var teamId = Guid.NewGuid();
        db.Teams.Add(new Team
        {
            Id = teamId,
            CommunityId = TestDbFactory.CommunityId,
            Name = "Mali Isler",
            NormalizedName = "MALI ISLER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        var seeded = TestDbFactory.SeedOpenApproverTask(db, member);
        seeded.Task.AssignmentType = TaskAssignmentType.Team;
        seeded.Task.CandidateTeamId = teamId;
        seeded.Task.Status = ProcessTaskStatus.Claimed;
        seeded.Task.ClaimedByUserId = teammate.Id;
        await db.SaveChangesAsync();
        var user = TestDbFactory.ToDto(member) with
        {
            Teams = [new UserTeamDto(teamId, "Mali Isler", false)]
        };

        var summary = await new DashboardService(db).GetSummaryAsync(user);

        Assert.Equal(0, summary.OpenTaskCount);
        Assert.Equal(0, summary.TeamQueueCount);
    }

    [Fact]
    public async Task Community_Admin_Personal_Summary_Focuses_On_Their_Teams_While_Community_Summary_Remains_Global()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.User, "dashboard-community-admin");
        var starter = TestDbFactory.SeedUser(db, Role.User, "dashboard-team-starter");
        var ownTeamId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        db.Teams.AddRange(
            new Team
            {
                Id = ownTeamId,
                CommunityId = TestDbFactory.CommunityId,
                Name = "Admin Team",
                NormalizedName = "ADMIN TEAM",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Team
            {
                Id = otherTeamId,
                CommunityId = TestDbFactory.CommunityId,
                Name = "Other Team",
                NormalizedName = "OTHER TEAM",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        var ownTeamWork = TestDbFactory.SeedOpenApproverTask(db, starter);
        ownTeamWork.Task.AssignmentType = TaskAssignmentType.Team;
        ownTeamWork.Task.CandidateTeamId = ownTeamId;
        var otherTeamWork = TestDbFactory.SeedOpenApproverTask(db, starter);
        otherTeamWork.Task.AssignmentType = TaskAssignmentType.Team;
        otherTeamWork.Task.CandidateTeamId = otherTeamId;
        await db.SaveChangesAsync();

        var user = TestDbFactory.CommunityAdminDto(admin) with
        {
            Teams = [new UserTeamDto(ownTeamId, "Admin Team", true)]
        };
        var service = new DashboardService(db);

        var personal = await service.GetSummaryAsync(user, WorkflowVisibilityScope.Personal);
        var community = await service.GetSummaryAsync(user, WorkflowVisibilityScope.Community);

        Assert.Equal(1, personal.OpenTaskCount);
        Assert.Equal(1, personal.TeamQueueCount);
        Assert.Equal(1, personal.InProgressProcessCount);
        Assert.Equal(2, community.OpenTaskCount);
        Assert.Equal(2, community.InProgressProcessCount);
        Assert.Null(community.TeamQueueCount);
    }
}
