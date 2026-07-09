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
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormFieldDefinition> FormFieldDefinitions => Set<FormFieldDefinition>();
    public DbSet<FieldValidationRule> FieldValidationRules => Set<FieldValidationRule>();
    public DbSet<ProcessInstance> ProcessInstances => Set<ProcessInstance>();
    public DbSet<ProcessTask> ProcessTasks => Set<ProcessTask>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemAuditLog> SystemAuditLogs => Set<SystemAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSqliteGuidConversion(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(user => user.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();
        modelBuilder.Entity<Community>().HasIndex(community => community.Name).IsUnique();
        modelBuilder.Entity<CommunityRole>().HasIndex(role => new { role.CommunityId, role.Name }).IsUnique();
        modelBuilder.Entity<CommunityRolePermission>().HasIndex(permission => new { permission.CommunityRoleId, permission.Permission }).IsUnique();
        modelBuilder.Entity<UserCommunityMembership>().HasIndex(membership => new { membership.UserId, membership.IsActive });

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

        modelBuilder.Entity<FormFieldDefinition>()
            .HasMany(field => field.ValidationRules)
            .WithOne(rule => rule.Field)
            .HasForeignKey(rule => rule.FormFieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProcessInstance>()
            .HasOne(process => process.Community)
            .WithMany()
            .HasForeignKey(process => process.CommunityId)
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
