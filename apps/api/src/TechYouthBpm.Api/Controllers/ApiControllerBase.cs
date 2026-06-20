using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

public abstract class ApiControllerBase(IAuthService authService) : ControllerBase
{
    protected IAuthService AuthService { get; } = authService;

    protected async Task<UserDto?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : authorization;

        return await AuthService.GetUserByTokenAsync(token, cancellationToken);
    }

    protected IActionResult UnauthorizedProblem() =>
        Unauthorized(new { errors = new[] { "A valid session token is required." } });

    protected IActionResult ValidationProblem(IReadOnlyList<string> errors) =>
        BadRequest(new { errors });
}
