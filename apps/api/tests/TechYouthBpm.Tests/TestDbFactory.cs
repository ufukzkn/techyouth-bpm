using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Tests;

internal static class TestDbFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static User SeedUser(AppDbContext db, Role role, string username = "")
    {
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
        db.SaveChanges();
        return user;
    }

    public static (ProcessInstance Process, ProcessTask Task) SeedOpenApproverTask(
        AppDbContext db,
        User startedByUser)
    {
        var formDefinition = new FormDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Form",
            Description = "Test form for unit tests",
            CreatedByUserId = startedByUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.FormDefinitions.Add(formDefinition);

        var process = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = formDefinition.Id,
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
            AssignedRole = Role.Approver,
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
}
