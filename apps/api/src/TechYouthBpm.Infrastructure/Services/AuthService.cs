using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

// Compatibility facade for existing service-level tests. Production DI resolves the focused contracts directly.
public sealed class AuthService : IAuthService
{
    private readonly IAuthenticationService authenticationService;
    private readonly IRegistrationService registrationService;
    private readonly IAccountService accountService;
    private readonly ISessionService sessionService;
    private readonly IUserAdministrationService userAdministrationService;

    public AuthService(
        AppDbContext db,
        IConfiguration configuration,
        ISystemAuditService auditService,
        IOtpService otpService,
        IEmailSender emailSender)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000 });
        var sessionCache = new SessionValidationCache(memoryCache, configuration);
        var authenticatedUserLoader = new AuthenticatedUserLoader(db, sessionCache);
        authenticationService = new AuthenticationService(
            db, configuration, auditService, otpService, emailSender, authenticatedUserLoader, sessionCache);
        registrationService = new RegistrationService(db, configuration, auditService, otpService, emailSender);
        accountService = new AccountService(db, configuration, auditService, otpService, emailSender, sessionCache);
        sessionService = new SessionService(db, configuration, auditService, otpService, emailSender, sessionCache);
        userAdministrationService = new UserAdministrationService(
            db, configuration, auditService, otpService, emailSender, sessionCache);
    }

    public AuthService(AppDbContext db, IConfiguration configuration)
        : this(db, configuration, new SystemAuditService(db), new OtpService(), new DemoEmailSender())
    {
    }

    public Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default) =>
        registrationService.RegisterAsync(request, cancellationToken);

    public Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default) =>
        authenticationService.LoginAsync(request, ipAddress, userAgent, cancellationToken);

    public Task<Result<LoginResponse>> RefreshSessionAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default) =>
        authenticationService.RefreshSessionAsync(refreshToken, ipAddress, userAgent, cancellationToken);

    public Task<UserDto?> GetUserByTokenAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        authenticationService.GetUserByTokenAsync(token, cancellationToken);

    public Task<Result<UserDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        accountService.UpdateProfileAsync(request, currentUser, cancellationToken);

    public Task<Result<UserDto>> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        accountService.ChangePasswordAsync(request, currentUser, cancellationToken);

    public Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default) =>
        accountService.ForgotPasswordAsync(request, cancellationToken);

    public Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default) =>
        accountService.ResetPasswordAsync(request, cancellationToken);

    public Task<Result<AdminPasswordResetResponse>> ResetPasswordByAdminAsync(
        Guid userId,
        AdminPasswordResetRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.ResetPasswordByAdminAsync(userId, request, currentUser, cancellationToken);

    public Task<Result<UserAdminDto>> CreateUserAsync(
        CreateUserRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.CreateUserAsync(request, currentUser, cancellationToken);

    public Task<Result> DeleteUserAsync(
        Guid userId,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.DeleteUserAsync(userId, currentUser, cancellationToken);

    public Task<Result<PagedResult<UserAdminDto>>> ListUsersAsync(
        UserDto currentUser,
        UserSearchRequest request,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.ListUsersAsync(currentUser, request, cancellationToken);

    public Task<Result<PagedResult<UserAdminDto>>> ListCommunityUsersAsync(
        Guid communityId,
        UserDto currentUser,
        UserSearchRequest request,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.ListCommunityUsersAsync(communityId, currentUser, request, cancellationToken);

    public Task<Result<UserAdminDto>> CreateCommunityUserAsync(
        Guid communityId,
        CreateUserRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.CreateCommunityUserAsync(communityId, request, currentUser, cancellationToken);

    public Task<Result<UserAdminDto>> UpdateCommunityMembershipAsync(
        Guid communityId,
        Guid userId,
        UpdateUserMembershipRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.UpdateCommunityMembershipAsync(
            communityId, userId, request, currentUser, cancellationToken);

    public Task<Result<UserAdminDto>> UpdateUserAccessAsync(
        Guid userId,
        UpdateUserAccessRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.UpdateUserAccessAsync(userId, request, currentUser, cancellationToken);

    public Task<Result<CommunityTransferPreviewDto>> PreviewCommunityTransferAsync(
        Guid userId,
        CommunityTransferPreviewRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.PreviewCommunityTransferAsync(
            userId, request, currentUser, cancellationToken);

    public Task<Result<UserAdminDto>> TransferCommunityAsync(
        Guid userId,
        CommunityTransferRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        userAdministrationService.TransferCommunityAsync(
            userId, request, currentUser, cancellationToken);

    public Task<Result<IReadOnlyList<UserSessionDto>>> ListSessionsAsync(
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default) =>
        sessionService.ListSessionsAsync(currentUser, currentToken, cancellationToken);

    public Task<Result<IReadOnlyList<UserSessionDto>>> ListUserSessionsAsync(
        Guid userId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default) =>
        sessionService.ListUserSessionsAsync(userId, currentUser, currentToken, cancellationToken);

    public Task<Result> LogoutAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        sessionService.LogoutAsync(token, cancellationToken);

    public Task<Result> RevokeSessionAsync(
        Guid sessionId,
        UserDto currentUser,
        string currentToken,
        CancellationToken cancellationToken = default) =>
        sessionService.RevokeSessionAsync(sessionId, currentUser, currentToken, cancellationToken);

    public Task<Result> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        sessionService.RevokeUserSessionAsync(userId, sessionId, currentUser, cancellationToken);

    public Task<Result<EmailVerificationStartResponse>> StartEmailVerificationAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        accountService.StartEmailVerificationAsync(currentUser, cancellationToken);

    public Task<Result<UserDto>> ConfirmEmailVerificationAsync(
        EmailVerificationConfirmRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default) =>
        accountService.ConfirmEmailVerificationAsync(request, currentUser, cancellationToken);

    public Task<Result<EmailVerificationStartResponse>> StartPublicEmailVerificationAsync(
        PublicEmailVerificationStartRequest request,
        CancellationToken cancellationToken = default) =>
        registrationService.StartPublicEmailVerificationAsync(request, cancellationToken);

    public Task<Result<RegisterResponse>> ConfirmPublicEmailVerificationAsync(
        PublicEmailVerificationConfirmRequest request,
        CancellationToken cancellationToken = default) =>
        registrationService.ConfirmPublicEmailVerificationAsync(request, cancellationToken);
}
