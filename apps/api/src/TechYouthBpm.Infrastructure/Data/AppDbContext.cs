using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TechYouthBpm.Domain.Entities;

namespace TechYouthBpm.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormFieldDefinition> FormFieldDefinitions => Set<FormFieldDefinition>();
    public DbSet<FieldValidationRule> FieldValidationRules => Set<FieldValidationRule>();
    public DbSet<ProcessInstance> ProcessInstances => Set<ProcessInstance>();
    public DbSet<ProcessTask> ProcessTasks => Set<ProcessTask>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSqliteGuidConversion(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(user => user.Username).IsUnique();
        modelBuilder.Entity<UserSession>().HasKey(session => session.Token);

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
            .HasMany(process => process.Tasks)
            .WithOne(task => task.ProcessInstance)
            .HasForeignKey(task => task.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProcessInstance>()
            .HasMany(process => process.AuditLogs)
            .WithOne(log => log.ProcessInstance)
            .HasForeignKey(log => log.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
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
