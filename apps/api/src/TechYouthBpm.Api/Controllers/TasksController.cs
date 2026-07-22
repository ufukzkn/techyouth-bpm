using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController(
    ITaskService taskService,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet("my")]
    public async Task<IActionResult> MyTasks([FromQuery] TaskListRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        return Ok(await taskService.ListMyTasksAsync(request, user, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var task = await taskService.GetAsync(id, user, cancellationToken);
        return task is null ? NotFound(new { errors = new[] { "Task was not found." } }) : Ok(task);
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<IActionResult> Execute(Guid id, TaskActionRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await taskService.ExecuteActionAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{id:guid}/claim")]
    public async Task<IActionResult> Claim(Guid id, ClaimTaskRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await taskService.ClaimAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, ClaimTaskRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await taskService.ReleaseAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpDelete("{id:guid}/claim")]
    public async Task<IActionResult> ReleaseClaim(Guid id, [FromBody] ClaimTaskRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await taskService.ReleaseAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
