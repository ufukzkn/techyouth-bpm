namespace TechYouthBpm.Application.Services;

// Compatibility aggregate for existing service-level tests and consumers.
// Production controllers depend on the focused contracts below.
public interface IAuthService :
    IAuthenticationService,
    IRegistrationService,
    IAccountService,
    ISessionService,
    IUserAdministrationService;
