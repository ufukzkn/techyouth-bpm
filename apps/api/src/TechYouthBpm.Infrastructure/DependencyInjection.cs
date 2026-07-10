using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=techyouth-bpm.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "postgresql":
                case "postgres":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider '{provider}'. Use 'Sqlite' or 'PostgreSql'.");
            }
        });

        services.AddScoped<IAuthService, AuthService>();
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
        services.AddScoped<IProcessService, ProcessService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ISystemAuditService, SystemAuditService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<ProcessStateMachine>();

        return services;
    }
}
