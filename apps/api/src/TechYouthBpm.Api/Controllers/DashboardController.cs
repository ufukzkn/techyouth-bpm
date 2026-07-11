using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(
    IDashboardService dashboardService,
    IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        return Ok(await dashboardService.GetSummaryAsync(user, cancellationToken));
    }
}
