using Microsoft.AspNetCore.Mvc;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Api.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController(
    IFormService formService,
    IFormVersionService formVersionService,
    IAuthenticationService authenticationService) : ApiControllerBase(authenticationService)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        return Ok(await formService.ListAsync(user, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var form = await formService.GetAsync(id, user, cancellationToken);
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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateFormRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await formService.UpdateAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        return user is null
            ? UnauthorizedProblem()
            : Ok(await formVersionService.ListVersionsAsync(id, user, cancellationToken));
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> GetVersion(Guid id, Guid versionId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var version = await formVersionService.GetVersionAsync(id, versionId, user, cancellationToken);
        return version is null ? NotFound(new { errors = new[] { "Form version was not found." } }) : Ok(version);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(
        Guid id,
        CreateFormVersionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await formVersionService.CreateDraftAsync(id, request, user, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPut("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> UpdateVersion(
        Guid id,
        Guid versionId,
        UpdateFormVersionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await formVersionService.UpdateAsync(id, versionId, request, user, cancellationToken);
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

        var result = await formVersionService.PublishAsync(id, versionId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/archive")]
    public async Task<IActionResult> ArchiveVersion(Guid id, Guid versionId, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem();
        }

        var result = await formVersionService.ArchiveAsync(id, versionId, user, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ValidationProblem(result.Errors);
    }
}
