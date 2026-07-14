using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController(
    ISystemAuditService systemAuditService,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet("system")]
    public async Task<IActionResult> System([FromQuery] SystemAuditSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await systemAuditService.ListAsync(user, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("system/counts")]
    public async Task<IActionResult> SystemCounts([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await systemAuditService.CountByCategoryAsync(user, query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
