using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

public abstract class ApiControllerBase(IAuthenticationService authenticationService) : ControllerBase
{
    protected IAuthenticationService AuthenticationService { get; } = authenticationService;

    protected string CurrentToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(authorization))
        {
            return authorization;
        }

        return Request.Cookies.TryGetValue(AuthCookieNames.AccessToken, out var cookieToken)
            ? cookieToken
            : string.Empty;
    }

    protected async Task<UserDto?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        return await AuthenticationService.GetUserByTokenAsync(CurrentToken(), cancellationToken);
    }

    protected IActionResult UnauthorizedProblem() =>
        Unauthorized(new { errors = new[] { "A valid session token is required." } });

    protected IActionResult ValidationProblem(IReadOnlyList<string> errors) =>
        BadRequest(new { errors });

    protected IActionResult ForbiddenProblem(IReadOnlyList<string> errors) =>
        StatusCode(StatusCodes.Status403Forbidden, new { errors });
}
