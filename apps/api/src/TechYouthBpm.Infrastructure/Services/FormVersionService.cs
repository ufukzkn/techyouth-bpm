using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class FormVersionService(AppDbContext db, ISystemAuditService auditService) : IFormVersionService
{
    public async Task<IReadOnlyList<FormDefinitionVersionDto>> ListVersionsAsync(
        Guid formDefinitionId,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.FormsView))
        {
            return [];
        }

        return (await ScopedVersionQuery(user)
                .Where(version => version.FormDefinitionId == formDefinitionId)
                .OrderByDescending(version => version.VersionNumber)
                .ToListAsync(cancellationToken))
            .Select(version => version.ToDto())
            .ToArray();
    }

    public async Task<FormDefinitionVersionDto?> GetVersionAsync(
        Guid formDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.FormsView))
        {
            return null;
        }

        var version = await ScopedVersionQuery(user).SingleOrDefaultAsync(
            item => item.Id == versionId && item.FormDefinitionId == formDefinitionId,
            cancellationToken);
        return version?.ToDto();
    }

    public async Task<Result<FormDefinitionVersionDto>> CreateDraftAsync(
        Guid formDefinitionId,
        CreateFormVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.FormsUpdate))
        {
            return Result<FormDefinitionVersionDto>.Failure("Current user cannot create form versions.");
        }

        var pages = request.Pages ?? [];
        var errors = FormVersionModel.ValidatePages(pages);
        if (errors.Count > 0)
        {
            return Result<FormDefinitionVersionDto>.Failure(errors);
        }

        var form = await ScopedFormQuery(user).SingleOrDefaultAsync(item => item.Id == formDefinitionId, cancellationToken);
        if (form is null)
        {
            return Result<FormDefinitionVersionDto>.Failure("Form definition was not found.");
        }

        if (!form.Community!.IsActive)
        {
            return Result<FormDefinitionVersionDto>.Failure("The form community is not active.");
        }

        var nextVersion = (await db.FormDefinitionVersions
            .Where(version => version.FormDefinitionId == formDefinitionId)
            .MaxAsync(version => (int?)version.VersionNumber, cancellationToken) ?? 0) + 1;
        var entity = new FormDefinitionVersion
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = form.Id,
            FormDefinition = form,
            VersionNumber = nextVersion,
            Status = DefinitionVersionStatus.Draft,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            Pages = FormVersionModel.BuildPages(pages)
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.FormDefinitionVersions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "FormDefinitionVersion.Created",
            "FormDefinitionVersion",
            entity.Id.ToString(),
            $"Draft version {entity.VersionNumber} was created for form '{form.Name}'.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FormDefinitionVersionDto>.Success(entity.ToDto());
    }

    public async Task<Result<FormDefinitionVersionDto>> UpdateAsync(
        Guid formDefinitionId,
        Guid versionId,
        UpdateFormVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.FormsUpdate))
        {
            return Result<FormDefinitionVersionDto>.Failure("Current user cannot update form versions.");
        }

        var pages = request.Pages ?? [];
        var errors = FormVersionModel.ValidatePages(pages);
        if (errors.Count > 0)
        {
            return Result<FormDefinitionVersionDto>.Failure(errors);
        }

        var version = await ScopedVersionQuery(user).SingleOrDefaultAsync(
            item => item.Id == versionId && item.FormDefinitionId == formDefinitionId,
            cancellationToken);
        if (version is null)
        {
            return Result<FormDefinitionVersionDto>.Failure("Form version was not found.");
        }

        if (version.Status == DefinitionVersionStatus.Published)
        {
            return await CreateDraftAsync(
                formDefinitionId,
                new CreateFormVersionRequest(pages),
                user,
                cancellationToken);
        }

        if (version.Status != DefinitionVersionStatus.Draft)
        {
            return Result<FormDefinitionVersionDto>.Failure("Only draft form versions can be updated.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var previousPages = version.Pages.ToArray();
        var replacementPages = FormVersionModel.BuildPages(pages);

        version.Pages.Clear();
        db.FormPageDefinitions.RemoveRange(previousPages);
        foreach (var page in replacementPages)
        {
            page.FormDefinitionVersionId = version.Id;
            version.Pages.Add(page);
        }

        db.FormPageDefinitions.AddRange(replacementPages);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "FormDefinitionVersion.Updated",
            "FormDefinitionVersion",
            version.Id.ToString(),
            $"Draft version {version.VersionNumber} of form '{version.FormDefinition!.Name}' was updated.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FormDefinitionVersionDto>.Success(version.ToDto());
    }

    public async Task<Result<FormDefinitionVersionDto>> PublishAsync(
        Guid formDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.FormsUpdate))
        {
            return Result<FormDefinitionVersionDto>.Failure("Current user cannot publish form versions.");
        }

        var version = await ScopedVersionQuery(user).SingleOrDefaultAsync(
            item => item.Id == versionId && item.FormDefinitionId == formDefinitionId,
            cancellationToken);
        if (version is null)
        {
            return Result<FormDefinitionVersionDto>.Failure("Form version was not found.");
        }

        if (version.Status == DefinitionVersionStatus.Published)
        {
            return Result<FormDefinitionVersionDto>.Success(version.ToDto());
        }

        if (version.Status != DefinitionVersionStatus.Draft)
        {
            return Result<FormDefinitionVersionDto>.Failure("Only draft form versions can be published.");
        }

        var errors = FormVersionModel.ValidatePages(ToRequests(version));
        if (errors.Count > 0)
        {
            return Result<FormDefinitionVersionDto>.Failure(errors);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        version.Status = DefinitionVersionStatus.Published;
        version.PublishedByUserId = user.Id;
        version.PublishedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "FormDefinitionVersion.Published",
            "FormDefinitionVersion",
            version.Id.ToString(),
            $"Version {version.VersionNumber} of form '{version.FormDefinition!.Name}' was published.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FormDefinitionVersionDto>.Success(version.ToDto());
    }

    public async Task<Result<FormDefinitionVersionDto>> ArchiveAsync(
        Guid formDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.FormsUpdate))
        {
            return Result<FormDefinitionVersionDto>.Failure("Current user cannot archive form versions.");
        }

        var version = await ScopedVersionQuery(user).SingleOrDefaultAsync(
            item => item.Id == versionId && item.FormDefinitionId == formDefinitionId,
            cancellationToken);
        if (version is null)
        {
            return Result<FormDefinitionVersionDto>.Failure("Form version was not found.");
        }

        if (version.Status == DefinitionVersionStatus.Archived)
        {
            return Result<FormDefinitionVersionDto>.Success(version.ToDto());
        }

        if (version.Status != DefinitionVersionStatus.Published)
        {
            return Result<FormDefinitionVersionDto>.Failure("Only published form versions can be archived.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        version.Status = DefinitionVersionStatus.Archived;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "FormDefinitionVersion.Archived",
            "FormDefinitionVersion",
            version.Id.ToString(),
            $"Version {version.VersionNumber} of form '{version.FormDefinition!.Name}' was archived.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FormDefinitionVersionDto>.Success(version.ToDto());
    }

    private IQueryable<FormDefinitionVersion> ScopedVersionQuery(UserDto user)
    {
        var query = db.FormDefinitionVersions
            .Include(version => version.FormDefinition)
            .ThenInclude(form => form!.Community)
            .Include(version => version.Pages)
            .ThenInclude(page => page.Fields)
            .ThenInclude(field => field.ValidationRules)
            .AsSplitQuery();

        return user.IsSuperAdmin()
            ? query
            : query.Where(version => version.FormDefinition != null && version.FormDefinition.CommunityId == user.CommunityId);
    }

    private IQueryable<FormDefinition> ScopedFormQuery(UserDto user)
    {
        var query = db.FormDefinitions.Include(form => form.Community);
        return user.IsSuperAdmin() ? query : query.Where(form => form.CommunityId == user.CommunityId);
    }

    private static IReadOnlyList<CreateFormPageRequest> ToRequests(FormDefinitionVersion version) =>
        version.Pages.Select(page => new CreateFormPageRequest(
            page.Key,
            page.Title,
            page.Description,
            page.SortOrder,
            page.Fields.Select(field => new CreateFormFieldRequest(
                field.Key,
                field.Label,
                field.Type,
                field.Required,
                field.SortOrder,
                JsonHelpers.Deserialize<IReadOnlyList<string>>(field.OptionsJson, []),
                field.ValidationRules.Select(rule => new ValidationRuleDto(
                    rule.RuleType,
                    rule.DependsOnFieldKey,
                    rule.ExpectedValue,
                    rule.Message)).ToArray())).ToArray())).ToArray();
}
