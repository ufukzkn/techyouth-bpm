using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController(
    ISystemAuditService systemAuditService,
    IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet("system")]
    public async Task<IActionResult> System(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await systemAuditService.ListAsync(user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
