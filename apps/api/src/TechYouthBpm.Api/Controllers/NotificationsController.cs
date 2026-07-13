using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(INotificationService notificationService, IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] NotificationListRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        return Ok(await notificationService.ListAsync(request, user, cancellationToken));
    }

    [HttpPatch("{notificationId:guid}/read-state")]
    public async Task<IActionResult> SetReadState(
        Guid notificationId,
        MarkNotificationReadStateRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await notificationService.SetReadStateAsync(notificationId, request.IsRead, user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await notificationService.MarkReadAsync(notificationId, user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await notificationService.MarkAllReadAsync(user, cancellationToken);
        return result.IsSuccess ? NoContent() : ValidationProblem(result.Errors);
    }
}
