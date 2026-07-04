using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

public abstract class ApiControllerBase(IAuthService authService) : ControllerBase
{
    protected IAuthService AuthService { get; } = authService;

    protected string CurrentToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : authorization;
    }

    protected async Task<UserDto?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        return await AuthService.GetUserByTokenAsync(CurrentToken(), cancellationToken);
    }

    protected IActionResult UnauthorizedProblem() =>
        Unauthorized(new { errors = new[] { "A valid session token is required." } });

    protected IActionResult ValidationProblem(IReadOnlyList<string> errors) =>
        BadRequest(new { errors });
}
