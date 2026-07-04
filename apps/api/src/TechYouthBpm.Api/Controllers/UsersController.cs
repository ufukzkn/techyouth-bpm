using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await AuthService.ListUsersAsync(user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
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

        var result = await AuthService.UpdateUserAccessAsync(userId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
