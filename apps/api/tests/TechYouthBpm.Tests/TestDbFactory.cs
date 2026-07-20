using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Tests;

internal static class TestDbFactory
{
    public static readonly Guid CommunityId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AdminCommunityRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid UserCommunityRoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid ApproverCommunityRoleId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid UnassignedCommunityRoleId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    public static AppDbContext Create(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .ConfigureWarnings(warnings =>
                warnings.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        var context = new AppDbContext(builder.Options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    public static User SeedUser(AppDbContext db, Role role, string username = "")
    {
        EnsureCommunityModel(db);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = string.IsNullOrEmpty(username) ? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}" : username,
            DisplayName = $"Test {role}",
            Email = $"{Guid.NewGuid():N}@test.local",
            Password = "test-hash",
            Role = role,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };

        db.Users.Add(user);
        db.UserCommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CommunityId = CommunityId,
            CommunityRoleId = role switch
            {
                Role.Approver => ApproverCommunityRoleId,
                Role.User => UserCommunityRoleId,
                _ => AdminCommunityRoleId
            },
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return user;
    }

    public static User SeedSuperAdmin(AppDbContext db, string username = "superadmin")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = "Test SuperAdmin",
            Email = $"{Guid.NewGuid():N}@test.local",
            Password = "test-hash",
            Role = Role.SuperAdmin,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static UserDto ToDto(User user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.Role,
            user.Status,
            user.IsEmailVerified,
            user.MustChangePassword,
            CommunityId,
            "Test Community",
            user.Role switch
            {
                Role.Approver => ApproverCommunityRoleId,
                Role.User => UserCommunityRoleId,
                _ => AdminCommunityRoleId
            },
            user.Role switch
            {
                Role.Approver => "Onay Sorumlusu",
                Role.User => "Surec Baslatici",
                _ => "Topluluk Admin"
            },
            PermissionsFor(user.Role));

    public static UserDto CommunityAdminDto(User user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            Role.User,
            user.Status,
            user.IsEmailVerified,
            user.MustChangePassword,
            CommunityId,
            "Test Community",
            AdminCommunityRoleId,
            "Topluluk Admin",
            PermissionNames.All);

    public static (ProcessInstance Process, ProcessTask Task) SeedOpenApproverTask(
        AppDbContext db,
        User startedByUser)
    {
        EnsureCommunityModel(db);
        db.SaveChanges();

        var formDefinition = new FormDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Form",
            Description = "Test form for unit tests",
            CommunityId = CommunityId,
            CreatedByUserId = startedByUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.FormDefinitions.Add(formDefinition);

        var process = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = formDefinition.Id,
            CommunityId = CommunityId,
            StartedByUserId = startedByUser.Id,
            Status = ProcessStatus.InProgress,
            FormDataJson = "{\"field1\": \"value1\"}",
            StartedAt = DateTime.UtcNow
        };
        db.ProcessInstances.Add(process);

        var task = new ProcessTask
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = process.Id,
            AssignedRole = Role.User,
            AssignedCommunityRoleId = ApproverCommunityRoleId,
            RequiredPermission = PermissionNames.TasksAct,
            Status = ProcessTaskStatus.Open,
            AvailableActionsJson = "[2,3]",
            CreatedAt = DateTime.UtcNow
        };
        db.ProcessTasks.Add(task);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = process.Id,
            UserId = startedByUser.Id,
            Action = WorkflowAction.Start,
            FromStatus = ProcessStatus.Pending,
            ToStatus = ProcessStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            Note = "Process started."
        });

        db.SaveChanges();
        return (process, task);
    }

    public static void EnsureCommunityModel(AppDbContext db)
    {
        if (!db.Communities.Any(item => item.Id == CommunityId))
        {
            db.Communities.Add(new Community
            {
                Id = CommunityId,
                Name = "Test Community",
                Description = "Unit test community",
                InviteCode = "TEST1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        EnsureRole(
            db,
            UnassignedCommunityRoleId,
            "Atanmadi",
            []);
        EnsureRole(
            db,
            AdminCommunityRoleId,
            "Topluluk Admin",
            [
                PermissionNames.CommunityManageUsers,
                PermissionNames.CommunityManageRoles,
                PermissionNames.CommunityManageAdmins,
                PermissionNames.TeamsView,
                PermissionNames.TeamsManage,
                PermissionNames.FormsView,
                PermissionNames.FormsCreate,
                PermissionNames.FormsUpdate,
                PermissionNames.ProcessesView,
                PermissionNames.ProcessesStart,
                PermissionNames.TasksView,
                PermissionNames.TasksAct,
                PermissionNames.AuditView
            ]);
        EnsureRole(
            db,
            UserCommunityRoleId,
            "Surec Baslatici",
            [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]);
        EnsureRole(
            db,
            ApproverCommunityRoleId,
            "Onay Sorumlusu",
            [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]);
    }

    private static void EnsureRole(AppDbContext db, Guid id, string name, IReadOnlyList<string> permissions)
    {
        if (db.CommunityRoles.Any(item => item.Id == id))
        {
            return;
        }

        db.CommunityRoles.Add(new CommunityRole
        {
            Id = id,
            CommunityId = CommunityId,
            Name = name,
            Description = $"{name} test role",
            TemplateKey = name == "Atanmadi" ? "unassigned" : name,
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow,
            Permissions = permissions.Select(permission => new CommunityRolePermission
            {
                Id = Guid.NewGuid(),
                Permission = permission
            }).ToList()
        });
    }

    private static IReadOnlyList<string> PermissionsFor(Role role) =>
        role switch
        {
            Role.Approver => [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct],
            Role.User => [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart],
            _ => PermissionNames.All
        };
}
