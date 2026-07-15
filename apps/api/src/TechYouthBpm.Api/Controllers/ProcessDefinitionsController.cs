using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/process-definitions")]
public class ProcessDefinitionsController(
    IProcessDefinitionService processDefinitionService,
    IProcessGraphValidator processGraphValidator,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        return user is null
            ? UnauthorizedProblem()
            : Ok(await processDefinitionService.ListAsync(user, cancellationToken));
    }

    [HttpGet("runnable")]
    public async Task<IActionResult> ListRunnable(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        return user is null
            ? UnauthorizedProblem()
            : Ok(await processDefinitionService.ListRunnableAsync(user, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var definition = await processDefinitionService.GetAsync(id, user, cancellationToken);
        return definition is null
            ? NotFound(new { errors = new[] { "Process definition was not found." } })
            : Ok(definition);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProcessDefinitionRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await processDefinitionService.CreateAsync(request, user, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : ValidationProblem(result.Errors);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProcessDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await processDefinitionService.UpdateAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(
        Guid id,
        CreateProcessDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await processDefinitionService.CreateVersionAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPut("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> UpdateVersion(
        Guid id,
        Guid versionId,
        UpdateProcessDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await processDefinitionService.UpdateVersionAsync(id, versionId, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/publish")]
    public async Task<IActionResult> PublishVersion(Guid id, Guid versionId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await processDefinitionService.PublishVersionAsync(id, versionId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<IActionResult> Validate(
        Guid id,
        CreateProcessDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        if (!user.HasPermission(PermissionNames.WorkflowsUpdate))
        {
            return ValidationProblem(["Current user cannot validate process definition versions."]);
        }

        var definition = await processDefinitionService.GetAsync(id, user, cancellationToken);
        if (definition is null)
        {
            return NotFound(new { errors = new[] { "Process definition was not found." } });
        }

        var result = await processGraphValidator.ValidateForPublishAsync(
            request.Graph,
            definition.CommunityId,
            request.FormDefinitionVersionId,
            cancellationToken);
        return Ok(new ProcessGraphValidationDto(result.IsSuccess, result.Errors));
    }
}
