using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController(ITaskService taskService, IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet("my")]
    public async Task<IActionResult> MyTasks(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        return Ok(await taskService.ListMyTasksAsync(user, cancellationToken));
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
}
