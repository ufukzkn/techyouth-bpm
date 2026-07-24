using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TechYouthBpm.Domain.Entities;

namespace TechYouthBpm.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<CommunityRole> CommunityRoles => Set<CommunityRole>();
    public DbSet<CommunityRolePermission> CommunityRolePermissions => Set<CommunityRolePermission>();
    public DbSet<UserCommunityMembership> UserCommunityMemberships => Set<UserCommunityMembership>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CommunityDeletionArchive> CommunityDeletionArchives => Set<CommunityDeletionArchive>();
    public DbSet<ArchivedAuditEvent> ArchivedAuditEvents => Set<ArchivedAuditEvent>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormFieldDefinition> FormFieldDefinitions => Set<FormFieldDefinition>();
    public DbSet<FieldValidationRule> FieldValidationRules => Set<FieldValidationRule>();
    public DbSet<FormDefinitionVersion> FormDefinitionVersions => Set<FormDefinitionVersion>();
    public DbSet<FormPageDefinition> FormPageDefinitions => Set<FormPageDefinition>();
    public DbSet<FormVersionFieldDefinition> FormVersionFieldDefinitions => Set<FormVersionFieldDefinition>();
    public DbSet<FormVersionFieldValidationRule> FormVersionFieldValidationRules => Set<FormVersionFieldValidationRule>();
    public DbSet<ProcessDefinition> ProcessDefinitions => Set<ProcessDefinition>();
    public DbSet<ProcessDefinitionVersion> ProcessDefinitionVersions => Set<ProcessDefinitionVersion>();
    public DbSet<ProcessInstance> ProcessInstances => Set<ProcessInstance>();
    public DbSet<ProcessTask> ProcessTasks => Set<ProcessTask>();
    public DbSet<ProcessStepExecution> ProcessStepExecutions => Set<ProcessStepExecution>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemAuditLog> SystemAuditLogs => Set<SystemAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSqliteGuidConversion(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(user => user.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();
        modelBuilder.Entity<Community>().HasIndex(community => community.Name).IsUnique();
        modelBuilder.Entity<Community>().HasIndex(community => community.InviteCode).IsUnique();
        modelBuilder.Entity<CommunityRole>().HasIndex(role => new { role.CommunityId, role.Name }).IsUnique();
        modelBuilder.Entity<CommunityRolePermission>().HasIndex(permission => new { permission.CommunityRoleId, permission.Permission }).IsUnique();
        modelBuilder.Entity<UserCommunityMembership>().HasIndex(membership => new { membership.UserId, membership.IsActive });
        modelBuilder.Entity<Team>().HasIndex(team => new { team.CommunityId, team.NormalizedName }).IsUnique();
        modelBuilder.Entity<TeamMembership>().HasIndex(membership => new { membership.TeamId, membership.UserId }).IsUnique();
        modelBuilder.Entity<TeamMembership>().HasIndex(membership => new { membership.UserId, membership.IsActive });
        modelBuilder.Entity<FormDefinitionVersion>().HasIndex(version => new { version.FormDefinitionId, version.VersionNumber }).IsUnique();
        modelBuilder.Entity<FormDefinitionVersion>().HasIndex(version => new { version.FormDefinitionId, version.Status });
        modelBuilder.Entity<FormPageDefinition>().HasIndex(page => new { page.FormDefinitionVersionId, page.Key }).IsUnique();
        modelBuilder.Entity<FormPageDefinition>().HasIndex(page => new { page.FormDefinitionVersionId, page.SortOrder });
        modelBuilder.Entity<FormVersionFieldDefinition>().HasIndex(field => new { field.FormPageDefinitionId, field.Key }).IsUnique();
        modelBuilder.Entity<ProcessDefinition>().HasIndex(definition => new { definition.CommunityId, definition.Name });
        modelBuilder.Entity<ProcessDefinitionVersion>().HasIndex(version => new { version.ProcessDefinitionId, version.VersionNumber }).IsUnique();
        modelBuilder.Entity<ProcessDefinitionVersion>().HasIndex(version => new { version.ProcessDefinitionId, version.Status });
        modelBuilder.Entity<ProcessStepExecution>().HasIndex(step => new { step.ProcessInstanceId, step.NodeKey, step.Attempt }).IsUnique();
        modelBuilder.Entity<ProcessInstance>().HasIndex(process => new { process.CommunityId, process.Status, process.StartedAt });
        modelBuilder.Entity<ProcessInstance>().HasIndex(process => new { process.StartedByUserId, process.Status, process.StartedAt });
        modelBuilder.Entity<ProcessTask>().HasIndex(task => new { task.Status, task.DueAt, task.Priority, task.CreatedAt });
        modelBuilder.Entity<ProcessTask>().HasIndex(task => new { task.AssignedUserId, task.Status });
        modelBuilder.Entity<ProcessTask>().HasIndex(task => new { task.ClaimedByUserId, task.Status });
        modelBuilder.Entity<ProcessTask>().HasIndex(task => new { task.CandidateTeamId, task.Status, task.ClaimedByUserId });
        modelBuilder.Entity<ProcessTask>().HasIndex(task => new { task.CandidateCommunityRoleId, task.Status, task.ClaimedByUserId });
        modelBuilder.Entity<ProcessTask>().Property(task => task.ClaimVersion).IsConcurrencyToken();

        modelBuilder.Entity<Community>()
            .HasMany(community => community.Roles)
            .WithOne(role => role.Community)
            .HasForeignKey(role => role.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CommunityRole>()
            .HasMany(role => role.Permissions)
            .WithOne(permission => permission.CommunityRole)
            .HasForeignKey(permission => permission.CommunityRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(user => user.CommunityMemberships)
            .WithOne(membership => membership.User)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserCommunityMembership>()
            .HasOne(membership => membership.Community)
            .WithMany()
            .HasForeignKey(membership => membership.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserCommunityMembership>()
            .HasOne(membership => membership.CommunityRole)
            .WithMany()
            .HasForeignKey(membership => membership.CommunityRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Community>()
            .HasMany(community => community.Teams)
            .WithOne(team => team.Community)
            .HasForeignKey(team => team.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Team>()
            .HasOne(team => team.CreatedByUser)
            .WithMany()
            .HasForeignKey(team => team.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Team>()
            .HasMany(team => team.Memberships)
            .WithOne(membership => membership.Team)
            .HasForeignKey(membership => membership.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(user => user.TeamMemberships)
            .WithOne(membership => membership.User)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Notification>()
            .HasOne(notification => notification.Community)
            .WithMany()
            .HasForeignKey(notification => notification.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Notification>().HasIndex(notification => new { notification.UserId, notification.ReadAt, notification.CreatedAt });
        modelBuilder.Entity<Notification>().HasIndex(notification => new { notification.UserId, notification.CreatedAt });
        modelBuilder.Entity<Notification>().HasIndex(notification => new { notification.CommunityId, notification.CreatedAt });
        modelBuilder.Entity<Notification>().HasIndex(notification => new
        {
            notification.UserId,
            notification.Type,
            notification.ReadAt,
            notification.CreatedAt
        });

        modelBuilder.Entity<UserSession>().HasKey(session => session.Id);
        modelBuilder.Entity<UserSession>().HasIndex(session => session.Token).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasKey(token => token.Id);
        modelBuilder.Entity<RefreshToken>().HasIndex(token => token.Token).IsUnique();
        modelBuilder.Entity<RefreshToken>()
            .HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RefreshToken>()
            .HasOne(token => token.UserSession)
            .WithMany()
            .HasForeignKey(token => token.UserSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormDefinition>()
            .HasOne(form => form.Community)
            .WithMany()
            .HasForeignKey(form => form.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FormDefinition>()
            .HasOne(form => form.UpdatedByUser)
            .WithMany()
            .HasForeignKey(form => form.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<FormDefinition>()
            .HasMany(form => form.Fields)
            .WithOne(field => field.FormDefinition)
            .HasForeignKey(field => field.FormDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormDefinition>()
            .HasMany(form => form.Versions)
            .WithOne(version => version.FormDefinition)
            .HasForeignKey(version => version.FormDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormDefinitionVersion>()
            .HasOne(version => version.CreatedByUser)
            .WithMany()
            .HasForeignKey(version => version.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FormDefinitionVersion>()
            .HasOne(version => version.PublishedByUser)
            .WithMany()
            .HasForeignKey(version => version.PublishedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<FormDefinitionVersion>()
            .HasMany(version => version.Pages)
            .WithOne(page => page.FormDefinitionVersion)
            .HasForeignKey(page => page.FormDefinitionVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormPageDefinition>()
            .HasMany(page => page.Fields)
            .WithOne(field => field.FormPageDefinition)
            .HasForeignKey(field => field.FormPageDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormVersionFieldDefinition>()
            .HasMany(field => field.ValidationRules)
            .WithOne(rule => rule.Field)
            .HasForeignKey(rule => rule.FormVersionFieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormFieldDefinition>()
            .HasMany(field => field.ValidationRules)
            .WithOne(rule => rule.Field)
            .HasForeignKey(rule => rule.FormFieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProcessDefinition>()
            .HasOne(definition => definition.Community)
            .WithMany()
            .HasForeignKey(definition => definition.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessDefinition>()
            .HasOne(definition => definition.CreatedByUser)
            .WithMany()
            .HasForeignKey(definition => definition.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessDefinition>()
            .HasOne(definition => definition.UpdatedByUser)
            .WithMany()
            .HasForeignKey(definition => definition.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessDefinition>()
            .HasMany(definition => definition.Versions)
            .WithOne(version => version.ProcessDefinition)
            .HasForeignKey(version => version.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProcessDefinitionVersion>()
            .HasOne(version => version.FormDefinitionVersion)
            .WithMany()
            .HasForeignKey(version => version.FormDefinitionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessDefinitionVersion>()
            .HasOne(version => version.CreatedByUser)
            .WithMany()
            .HasForeignKey(version => version.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessDefinitionVersion>()
            .HasOne(version => version.PublishedByUser)
            .WithMany()
            .HasForeignKey(version => version.PublishedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(process => process.Community)
            .WithMany()
            .HasForeignKey(process => process.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(process => process.FormDefinitionVersion)
            .WithMany()
            .HasForeignKey(process => process.FormDefinitionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(process => process.ProcessDefinitionVersion)
            .WithMany()
            .HasForeignKey(process => process.ProcessDefinitionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasMany(process => process.Tasks)
            .WithOne(task => task.ProcessInstance)
            .HasForeignKey(task => task.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(task => task.AssignedCommunityRole)
            .WithMany()
            .HasForeignKey(task => task.AssignedCommunityRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(task => task.AssignedUser)
            .WithMany()
            .HasForeignKey(task => task.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(task => task.CandidateTeam)
            .WithMany()
            .HasForeignKey(task => task.CandidateTeamId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(task => task.CandidateCommunityRole)
            .WithMany()
            .HasForeignKey(task => task.CandidateCommunityRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(task => task.ClaimedByUser)
            .WithMany()
            .HasForeignKey(task => task.ClaimedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessTask>()
            .HasOne(task => task.FormDefinitionVersion)
            .WithMany()
            .HasForeignKey(task => task.FormDefinitionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProcessInstance>()
            .HasMany(process => process.StepExecutions)
            .WithOne(step => step.ProcessInstance)
            .HasForeignKey(step => step.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProcessStepExecution>()
            .HasOne(step => step.CompletedByUser)
            .WithMany()
            .HasForeignKey(step => step.CompletedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProcessInstance>()
            .HasMany(process => process.AuditLogs)
            .WithOne(log => log.ProcessInstance)
            .HasForeignKey(log => log.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SystemAuditLog>()
            .HasOne(log => log.ActorUser)
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SystemAuditLog>()
            .HasOne(log => log.Community)
            .WithMany()
            .HasForeignKey(log => log.CommunityId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SystemAuditLog>()
            .HasIndex(log => new { log.CommunityId, log.Category, log.CreatedAt });

        modelBuilder.Entity<SystemAuditLog>()
            .Property(log => log.Category)
            .HasMaxLength(32);

        modelBuilder.Entity<CommunityDeletionArchive>()
            .HasIndex(archive => archive.DeletedAt);
        modelBuilder.Entity<CommunityDeletionArchive>()
            .HasIndex(archive => archive.OriginalCommunityId);
        modelBuilder.Entity<CommunityDeletionArchive>()
            .HasMany(archive => archive.Events)
            .WithOne(auditEvent => auditEvent.CommunityDeletionArchive)
            .HasForeignKey(auditEvent => auditEvent.CommunityDeletionArchiveId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivedAuditEvent>()
            .HasIndex(auditEvent => new
            {
                auditEvent.CommunityDeletionArchiveId,
                auditEvent.Category,
                auditEvent.OccurredAt
            });
        modelBuilder.Entity<ArchivedAuditEvent>()
            .Property(auditEvent => auditEvent.Category)
            .HasMaxLength(32);
        modelBuilder.Entity<ArchivedAuditEvent>()
            .Property(auditEvent => auditEvent.Source)
            .HasMaxLength(24);
    }

    private void ConfigureSqliteGuidConversion(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
        {
            return;
        }

        var guidConverter = new ValueConverter<Guid, string>(
            value => value.ToString("D"),
            value => Guid.Parse(value));

        var nullableGuidConverter = new ValueConverter<Guid?, string?>(
            value => value.HasValue ? value.Value.ToString("D") : null,
            value => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Guid))
                {
                    property.SetValueConverter(guidConverter);
                }
                else if (property.ClrType == typeof(Guid?))
                {
                    property.SetValueConverter(nullableGuidConverter);
                }
            }
        }
    }
}
