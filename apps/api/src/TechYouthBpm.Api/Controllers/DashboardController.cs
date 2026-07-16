using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(
    IDashboardService dashboardService,
    IWorkflowVisibilityService workflowVisibilityService,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] string scope = "personal",
        CancellationToken cancellationToken = default)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var resolvedScope = workflowVisibilityService.ResolveScope(scope, user);
        if (!resolvedScope.IsSuccess)
        {
            return ForbiddenProblem(resolvedScope.Errors);
        }

        return Ok(await dashboardService.GetSummaryAsync(
            user,
            resolvedScope.Value,
            cancellationToken));
    }
}
