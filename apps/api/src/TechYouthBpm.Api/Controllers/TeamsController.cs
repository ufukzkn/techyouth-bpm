using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Application.Teams;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController(
    ITeamService teamService,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TeamSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.ListAsync(request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{teamId:guid}")]
    public async Task<IActionResult> Get(Guid teamId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.GetAsync(teamId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.CreateAsync(request, user, cancellationToken);
        return result.IsSuccess ? Created($"/api/teams/{result.Value!.Id}", result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{teamId:guid}")]
    public async Task<IActionResult> Update(Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.UpdateAsync(teamId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{teamId:guid}/members")]
    public async Task<IActionResult> Members(Guid teamId, [FromQuery] TeamMemberSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.ListMembersAsync(teamId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{teamId:guid}/roster")]
    public async Task<IActionResult> Roster(Guid teamId, [FromQuery] TeamMemberSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.ListRosterAsync(teamId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{teamId:guid}/members/{userId:guid}/tasks")]
    public async Task<IActionResult> MemberTasks(
        Guid teamId,
        Guid userId,
        [FromQuery] TeamMemberTaskSearchRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.ListMemberTasksAsync(teamId, userId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{teamId:guid}/candidates")]
    public async Task<IActionResult> Candidates(Guid teamId, [FromQuery] TeamMemberSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.ListCandidatesAsync(teamId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("unassigned/members")]
    public async Task<IActionResult> Unassigned([FromQuery] UnassignedTeamMemberSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.ListUnassignedAsync(request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{teamId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid teamId, AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.AddMemberAsync(teamId, request, user, cancellationToken);
        return result.IsSuccess ? Created($"/api/teams/{teamId}/members/{request.UserId}", result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid teamId, Guid userId, UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.UpdateMemberAsync(teamId, userId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpDelete("{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return UnauthorizedProblem();
        var result = await teamService.RemoveMemberAsync(teamId, userId, user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }
}
