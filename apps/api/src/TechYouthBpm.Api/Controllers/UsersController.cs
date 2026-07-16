using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(
    IUserAdministrationService userAdministrationService,
    ISessionService sessionService,
    ITeamService teamService,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await userAdministrationService.ListUsersAsync(user, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await userAdministrationService.CreateUserAsync(request, user, cancellationToken);
        return result.IsSuccess ? Created($"/api/users/{result.Value!.Id}", result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{userId:guid}/access")]
    public async Task<IActionResult> UpdateAccess(
        Guid userId,
        UpdateUserAccessRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await userAdministrationService.UpdateUserAccessAsync(userId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await userAdministrationService.DeleteUserAsync(userId, user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [HttpPost("{userId:guid}/password-reset-by-admin")]
    public async Task<IActionResult> ResetPasswordByAdmin(
        Guid userId,
        AdminPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await userAdministrationService.ResetPasswordByAdminAsync(userId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{userId:guid}/sessions")]
    public async Task<IActionResult> Sessions(Guid userId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await sessionService.ListUserSessionsAsync(userId, user, CurrentToken(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{userId:guid}/team-memberships")]
    public async Task<IActionResult> TeamMemberships(Guid userId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await teamService.ListUserMembershipsAsync(userId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpDelete("{userId:guid}/sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await sessionService.RevokeUserSessionAsync(userId, sessionId, user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }
}
