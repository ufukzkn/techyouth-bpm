using Microsoft.Extensions.DependencyInjection;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Tests.Integration;

public sealed class AuthServiceRegistrationIntegrationTests
{
    [Fact]
    public void Production_container_resolves_focused_auth_services_without_aggregate_registration()
    {
        using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var focusedServices = new object[]
        {
            provider.GetRequiredService<IAuthenticationService>(),
            provider.GetRequiredService<IRegistrationService>(),
            provider.GetRequiredService<IAccountService>(),
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<IUserAdministrationService>(),
        };

        Assert.Equal(5, focusedServices.Select(service => service.GetType()).Distinct().Count());
        Assert.Null(provider.GetService<IAuthService>());
    }
}
