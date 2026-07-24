using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController(
    ISystemAuditService systemAuditService,
    IAuditArchiveService auditArchiveService,
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

    [HttpGet("archives")]
    public async Task<IActionResult> Archives(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }
        if (!user.IsSuperAdmin())
        {
            return ForbiddenProblem(["Only SuperAdmin users can view deleted community archives."]);
        }

        var result = await auditArchiveService.ListArchivesAsync(user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("archives/{archiveId:guid}/logs")]
    public async Task<IActionResult> ArchiveLogs(
        Guid archiveId,
        [FromQuery] SystemAuditSearchRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }
        if (!user.IsSuperAdmin())
        {
            return ForbiddenProblem(["Only SuperAdmin users can view deleted community archives."]);
        }

        var result = await auditArchiveService.ListEventsAsync(
            archiveId,
            user,
            request,
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("archives/{archiveId:guid}/counts")]
    public async Task<IActionResult> ArchiveCounts(Guid archiveId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }
        if (!user.IsSuperAdmin())
        {
            return ForbiddenProblem(["Only SuperAdmin users can view deleted community archives."]);
        }

        var result = await auditArchiveService.CountEventsByCategoryAsync(
            archiveId,
            user,
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
