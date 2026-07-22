using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Integration;

public sealed class QueryRegressionTests
{
    [Fact]
    public async Task Workflow_scope_queries_use_their_composite_sqlite_indexes()
    {
        await using var db = TestDbFactory.Create();

        var processPlan = await ExplainAsync(
            db.Database.GetDbConnection(),
            "EXPLAIN QUERY PLAN SELECT Id FROM ProcessInstances WHERE CommunityId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AND Status = 2 ORDER BY StartedAt DESC");
        var taskPlan = await ExplainAsync(
            db.Database.GetDbConnection(),
            "EXPLAIN QUERY PLAN SELECT Id FROM ProcessTasks WHERE CandidateTeamId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AND Status = 1 AND ClaimedByUserId IS NULL");

        Assert.Contains(processPlan, line => line.Contains("IX_ProcessInstances_CommunityId_Status_StartedAt", StringComparison.Ordinal));
        Assert.Contains(taskPlan, line => line.Contains("IX_ProcessTasks_CandidateTeamId_Status_ClaimedByUserId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Opaque_session_validation_uses_cache_and_logout_invalidates_it()
    {
        var counter = new CommandCountingInterceptor();
        await using var db = TestDbFactory.Create(counter);
        var user = TestDbFactory.SeedUser(db, Role.User, "cached-session-user");
        const string rawToken = "opaque-session-cache-test-token";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AuthService(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:SessionCacheSeconds"] = "30",
        }).Build());

        counter.Reset();
        Assert.NotNull(await service.GetUserByTokenAsync(rawToken));
        Assert.True(counter.Count > 0);

        counter.Reset();
        Assert.NotNull(await service.GetUserByTokenAsync(rawToken));
        Assert.Equal(0, counter.Count);

        Assert.True((await service.LogoutAsync(rawToken)).IsSuccess);
        counter.Reset();
        Assert.Null(await service.GetUserByTokenAsync(rawToken));
        Assert.True(counter.Count > 0);
    }

    [Fact]
    public async Task User_search_paginates_large_fixture_with_bounded_query_count()
    {
        var counter = new CommandCountingInterceptor();
        await using var db = TestDbFactory.Create(counter);
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "query-superadmin");
        TestDbFactory.EnsureCommunityModel(db);
        db.Users.AddRange(Enumerable.Range(1, 250).Select(index => new User
        {
            Id = Guid.NewGuid(),
            Username = $"fixture-user-{index:000}",
            DisplayName = $"Fixture User {index:000}",
            Email = $"fixture-user-{index:000}@test.local",
            Password = "test-hash",
            Role = Role.User,
            Status = index % 3 == 0 ? UserStatus.PendingApproval : UserStatus.Active,
            IsEmailVerified = true,
            CommunityMemberships =
            [
                new UserCommunityMembership
                {
                    Id = Guid.NewGuid(),
                    CommunityId = TestDbFactory.CommunityId,
                    CommunityRoleId = TestDbFactory.UserCommunityRoleId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                },
            ],
        }));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        counter.Reset();

        var service = new AuthService(db, new ConfigurationBuilder().Build());
        var result = await service.ListUsersAsync(
            TestDbFactory.ToDto(superAdmin),
            new UserSearchRequest(Query: "fixture-user", Page: 7, PageSize: 25));

        Assert.True(result.IsSuccess);
        Assert.Equal(250, result.Value!.TotalCount);
        Assert.Equal(25, result.Value.Items.Count);
        Assert.Equal(7, result.Value.Page);
        Assert.InRange(counter.Count, 1, 8);
    }

    [Fact]
    public async Task Audit_search_paginates_large_fixture_with_bounded_query_count()
    {
        var counter = new CommandCountingInterceptor();
        await using var db = TestDbFactory.Create(counter);
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "audit-query-superadmin");
        db.SystemAuditLogs.AddRange(Enumerable.Range(1, 500).Select(index => new SystemAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = superAdmin.Id,
            Category = index % 2 == 0 ? SystemAuditCategories.Identity : SystemAuditCategories.Processes,
            Action = index % 2 == 0 ? "Auth.LoginSucceeded" : "Process.Started",
            EntityType = index % 2 == 0 ? "Session" : "ProcessInstance",
            EntityId = Guid.NewGuid().ToString(),
            Description = $"Large audit fixture {index:000}",
            CreatedAt = DateTime.UtcNow.AddSeconds(-index),
        }));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        counter.Reset();

        var result = await new SystemAuditService(db).ListAsync(
            TestDbFactory.ToDto(superAdmin),
            new SystemAuditSearchRequest(
                Query: "Large audit fixture",
                Category: SystemAuditCategories.Processes,
                Page: 8,
                PageSize: 25));

        Assert.True(result.IsSuccess);
        Assert.Equal(250, result.Value!.TotalCount);
        Assert.Equal(25, result.Value.Items.Count);
        Assert.Equal(8, result.Value.Page);
        Assert.InRange(counter.Count, 1, 4);
    }

    [Fact]
    public async Task Audit_category_counts_use_one_conditional_aggregate_query()
    {
        var counter = new CommandCountingInterceptor();
        await using var db = TestDbFactory.Create(counter);
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "audit-count-superadmin");
        db.SystemAuditLogs.AddRange(Enumerable.Range(1, 60).Select(index => new SystemAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = superAdmin.Id,
            Category = index % 3 == 0
                ? SystemAuditCategories.Identity
                : index % 3 == 1
                    ? SystemAuditCategories.Processes
                    : SystemAuditCategories.Tasks,
            Action = "Fixture.Action",
            EntityType = "Fixture",
            Description = $"Audit count fixture {index:000}",
            CreatedAt = DateTime.UtcNow.AddSeconds(-index),
        }));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        counter.Reset();

        var result = await new SystemAuditService(db).CountByCategoryAsync(TestDbFactory.ToDto(superAdmin));

        Assert.True(result.IsSuccess);
        Assert.Equal(60, result.Value!.All);
        Assert.Equal(20, result.Value.Identity);
        Assert.Equal(20, result.Value.Processes);
        Assert.Equal(20, result.Value.Tasks);
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task Process_and_task_lists_keep_large_fixtures_server_paged_with_bounded_queries()
    {
        var counter = new CommandCountingInterceptor();
        await using var db = TestDbFactory.Create(counter);
        var manager = TestDbFactory.SeedUser(db, Role.Admin, "process-query-manager");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "process-query-approver");
        var form = new FormDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Large Query Form",
            Description = "Query regression fixture",
            CommunityId = TestDbFactory.CommunityId,
            CreatedByUserId = manager.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.FormDefinitions.Add(form);
        for (var index = 1; index <= 180; index += 1)
        {
            var process = new ProcessInstance
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = form.Id,
                CommunityId = TestDbFactory.CommunityId,
                StartedByUserId = manager.Id,
                Status = ProcessStatus.InProgress,
                FormDataJson = "{}",
                StartedAt = DateTime.UtcNow.AddMinutes(-index),
            };
            db.ProcessInstances.Add(process);
            db.ProcessTasks.Add(new ProcessTask
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = process.Id,
                AssignedRole = Role.User,
                AssignedCommunityRoleId = TestDbFactory.ApproverCommunityRoleId,
                CandidateCommunityRoleId = TestDbFactory.ApproverCommunityRoleId,
                RequiredPermission = PermissionNames.TasksAct,
                Status = ProcessTaskStatus.Open,
                AvailableActionsJson = "[2,3]",
                Priority = index % 4 == 0 ? TaskPriority.High : TaskPriority.Normal,
                DueAt = DateTime.UtcNow.AddHours(index),
                CreatedAt = DateTime.UtcNow.AddMinutes(-index),
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        counter.Reset();
        var processResult = await new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db)).ListAsync(
                new ProcessListRequest(Page: 4, PageSize: 25, Scope: "community"),
                TestDbFactory.CommunityAdminDto(manager));
        Assert.Equal(180, processResult.TotalCount);
        Assert.Equal(25, processResult.Items.Count);
        Assert.InRange(counter.Count, 1, 12);

        counter.Reset();
        var taskResult = await new TaskService(db, new ProcessStateMachine()).ListMyTasksAsync(
            new TaskListRequest(Page: 5, PageSize: 25),
            TestDbFactory.ToDto(approver));
        Assert.Equal(180, taskResult.TotalCount);
        Assert.Equal(25, taskResult.Items.Count);
        Assert.All(taskResult.Items, task => Assert.Null(task.TaskForm));
        Assert.Equal(2, counter.Count);
    }

    private static async Task<IReadOnlyList<string>> ExplainAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(3));
        }

        return lines;
    }
}

internal sealed class CommandCountingInterceptor : DbCommandInterceptor
{
    private int count;

    public int Count => Volatile.Read(ref count);

    public void Reset() => Interlocked.Exchange(ref count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref count);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Interlocked.Increment(ref count);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
