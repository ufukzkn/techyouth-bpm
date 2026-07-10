using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/communities")]
public class CommunitiesController(ICommunityService communityService, IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.ListAsync(user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.CreateAsync(request, user, cancellationToken);
        return result.IsSuccess ? Created($"/api/communities/{result.Value!.Id}", result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{communityId:guid}")]
    public async Task<IActionResult> Update(Guid communityId, UpdateCommunityRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.UpdateAsync(communityId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{communityId:guid}/invite-code/regenerate")]
    public async Task<IActionResult> RegenerateInviteCode(Guid communityId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.RegenerateInviteCodeAsync(communityId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{communityId:guid}/summary")]
    public async Task<IActionResult> Summary(Guid communityId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.GetSummaryAsync(communityId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("role-templates")]
    public async Task<IActionResult> RoleTemplates(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.ListRoleTemplatesAsync(user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{communityId:guid}/roles")]
    public async Task<IActionResult> Roles(Guid communityId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.ListRolesAsync(communityId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{communityId:guid}/roles")]
    public async Task<IActionResult> CreateRole(Guid communityId, CreateCommunityRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.CreateRoleAsync(communityId, request, user, cancellationToken);
        return result.IsSuccess ? Created($"/api/communities/{communityId}/roles/{result.Value!.Id}", result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{communityId:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(Guid communityId, Guid roleId, UpdateCommunityRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.UpdateRoleAsync(communityId, roleId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpDelete("{communityId:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(
        Guid communityId,
        Guid roleId,
        DeleteCommunityRoleRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.DeleteRoleAsync(communityId, roleId, request, user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [HttpGet("{communityId:guid}/users")]
    public async Task<IActionResult> Users(Guid communityId, [FromQuery] UserSearchRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.ListUsersAsync(communityId, user, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{communityId:guid}/users")]
    public async Task<IActionResult> CreateUser(Guid communityId, CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.CreateUserAsync(communityId, request, user, cancellationToken);
        return result.IsSuccess ? Created($"/api/users/{result.Value!.Id}", result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPatch("{communityId:guid}/users/{userId:guid}/membership")]
    public async Task<IActionResult> UpdateMembership(
        Guid communityId,
        Guid userId,
        UpdateUserMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await communityService.UpdateMembershipAsync(communityId, userId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
