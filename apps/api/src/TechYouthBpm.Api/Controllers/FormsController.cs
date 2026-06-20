using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController(IFormService formService, IAuthService authService) : ApiControllerBase(authService)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        return Ok(await formService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var form = await formService.GetAsync(id, cancellationToken);
        return form is null ? NotFound(new { errors = new[] { "Form was not found." } }) : Ok(form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFormRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await formService.CreateAsync(request, user, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : ValidationProblem(result.Errors);
    }
}
