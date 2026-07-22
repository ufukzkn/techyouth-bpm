using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechYouthBpm.Application.Health;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Health;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=techyouth-bpm.db";
        var databaseSchema = configuration["Database:Schema"];

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "postgresql":
                case "postgres":
                    options.UseNpgsql(connectionString, providerOptions =>
                    {
                        if (!string.IsNullOrWhiteSpace(databaseSchema))
                        {
                            providerOptions.MigrationsHistoryTable(
                                HistoryRepository.DefaultTableName,
                                databaseSchema);
                        }
                    });
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider '{provider}'. Use 'Sqlite' or 'PostgreSql'.");
            }
        });

        services.AddMemoryCache(options => options.SizeLimit = 10_000);
        services.AddSingleton<ISessionValidationCache, SessionValidationCache>();
        services.AddScoped<ISystemReadinessService, SystemReadinessService>();
        services.AddScoped<AuthenticatedUserLoader>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IEmailSender>(_ =>
        {
            var emailProvider = configuration["Email:Provider"] ?? "Demo";
            return emailProvider.Trim().ToLowerInvariant() switch
            {
                "smtp" or "mailtrap" => new SmtpEmailSender(configuration),
                "routing" => new RoutingEmailSender(configuration),
                "demo" => new DemoEmailSender(),
                _ => throw new InvalidOperationException(
                    $"Unsupported email provider '{emailProvider}'. Use 'Demo', 'Smtp', 'Mailtrap' or 'Routing'.")
            };
        });
        services.AddScoped<IFormService, FormService>();
        services.AddScoped<IFormVersionService, FormVersionService>();
        services.AddScoped<IProcessGraphValidator, ProcessGraphValidator>();
        services.AddScoped<IProcessDefinitionService, ProcessDefinitionService>();
        services.AddSingleton<TaskAccessPolicy>();
        services.AddScoped<IWorkflowVisibilityService, WorkflowVisibilityService>();
        services.AddScoped<IProcessService, ProcessService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ISystemAuditService, SystemAuditService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<ICommunityRoleService>(provider =>
            (ICommunityRoleService)provider.GetRequiredService<ICommunityService>());
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddSingleton<ProcessStateMachine>();

        return services;
    }
}
