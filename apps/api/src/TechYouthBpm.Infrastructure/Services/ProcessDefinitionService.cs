using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class ProcessDefinitionService(
    AppDbContext db,
    IProcessGraphValidator graphValidator,
    ISystemAuditService auditService) : IProcessDefinitionService
{
    public async Task<IReadOnlyList<ProcessDefinitionSummaryDto>> ListAsync(
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsView))
        {
            return [];
        }

        var definitions = await ScopedQuery(user)
            .AsNoTracking()
            .OrderBy(definition => definition.Name)
            .ToListAsync(cancellationToken);

        return definitions.Select(definition =>
        {
            var latest = definition.Versions.OrderByDescending(version => version.VersionNumber).FirstOrDefault();
            var published = definition.Versions
                .Where(version => version.Status == DefinitionVersionStatus.Published)
                .OrderByDescending(version => version.VersionNumber)
                .FirstOrDefault();
            return new ProcessDefinitionSummaryDto(
                definition.Id,
                definition.Name,
                definition.Description,
                definition.CommunityId,
                definition.Community?.Name ?? string.Empty,
                latest?.VersionNumber,
                published?.Id,
                published?.FormDefinitionVersionId,
                definition.CreatedAt);
        }).ToArray();
    }

    public async Task<IReadOnlyList<RunnableProcessDefinitionDto>> ListRunnableAsync(
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.ProcessesStart))
        {
            return [];
        }

        var query = db.ProcessDefinitions
            .AsNoTracking()
            .Where(definition => definition.Community != null && definition.Community.IsActive);
        if (!user.IsSuperAdmin())
        {
            query = query.Where(definition => definition.CommunityId == user.CommunityId);
        }

        var definitions = await query
            .Include(definition => definition.Community)
            .Include(definition => definition.Versions)
            .OrderBy(definition => definition.Name)
            .ToListAsync(cancellationToken);

        return definitions
            .Select(definition => new
            {
                Definition = definition,
                Version = definition.Versions
                    .Where(version => version.Status == DefinitionVersionStatus.Published)
                    .OrderByDescending(version => version.VersionNumber)
                    .FirstOrDefault()
            })
            .Where(item => item.Version is not null)
            .Select(item => new RunnableProcessDefinitionDto(
                item.Definition.Id,
                item.Definition.Name,
                item.Definition.Description,
                item.Definition.CommunityId,
                item.Definition.Community?.Name ?? string.Empty,
                item.Version!.Id,
                item.Version.FormDefinitionVersionId,
                item.Version.VersionNumber))
            .ToArray();
    }

    public async Task<ProcessDefinitionDto?> GetAsync(
        Guid id,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsView))
        {
            return null;
        }

        var definition = await ScopedQuery(user)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return definition?.ToDto();
    }

    public async Task<Result<ProcessDefinitionDto>> CreateAsync(
        CreateProcessDefinitionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsCreate))
        {
            return Result<ProcessDefinitionDto>.Failure("Current user cannot create process definitions.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ProcessDefinitionDto>.Failure("Process definition name is required.");
        }

        var communityId = user.IsSuperAdmin() ? request.CommunityId ?? user.CommunityId : user.CommunityId;
        if (communityId is null)
        {
            return Result<ProcessDefinitionDto>.Failure("A community is required for process definitions.");
        }

        if (!await db.Communities.AnyAsync(
                community => community.Id == communityId && community.IsActive,
                cancellationToken))
        {
            return Result<ProcessDefinitionDto>.Failure("The process community is not active.");
        }

        var normalizedName = request.Name.Trim();
        if (await db.ProcessDefinitions.AnyAsync(
                definition => definition.CommunityId == communityId
                    && definition.Name.ToLower() == normalizedName.ToLower(),
                cancellationToken))
        {
            return Result<ProcessDefinitionDto>.Failure("A process definition with this name already exists in the community.");
        }

        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = request.Description?.Trim() ?? string.Empty,
            CommunityId = communityId.Value,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "ProcessDefinition.Created",
            "ProcessDefinition",
            definition.Id.ToString(),
            $"Process definition '{definition.Name}' was created.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDefinitionDto>.Success((await GetAsync(definition.Id, user, cancellationToken))!);
    }

    public async Task<Result<ProcessDefinitionDto>> UpdateAsync(
        Guid id,
        UpdateProcessDefinitionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsUpdate))
        {
            return Result<ProcessDefinitionDto>.Failure("Current user cannot update process definitions.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ProcessDefinitionDto>.Failure("Process definition name is required.");
        }

        var definition = await ScopedQuery(user).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (definition is null)
        {
            return Result<ProcessDefinitionDto>.Failure("Process definition was not found.");
        }

        var normalizedName = request.Name.Trim();
        if (await db.ProcessDefinitions.AnyAsync(
                item => item.Id != id
                    && item.CommunityId == definition.CommunityId
                    && item.Name.ToLower() == normalizedName.ToLower(),
                cancellationToken))
        {
            return Result<ProcessDefinitionDto>.Failure("A process definition with this name already exists in the community.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        definition.Name = normalizedName;
        definition.Description = request.Description?.Trim() ?? string.Empty;
        definition.UpdatedByUserId = user.Id;
        definition.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "ProcessDefinition.Updated",
            "ProcessDefinition",
            definition.Id.ToString(),
            $"Process definition '{definition.Name}' was updated.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDefinitionDto>.Success((await GetAsync(definition.Id, user, cancellationToken))!);
    }

    public async Task<Result<ProcessDefinitionVersionDto>> CreateVersionAsync(
        Guid processDefinitionId,
        CreateProcessDefinitionVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsUpdate))
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Current user cannot create process definition versions.");
        }

        var definition = await ScopedQuery(user)
            .SingleOrDefaultAsync(item => item.Id == processDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Process definition was not found.");
        }

        if (!await FormVersionBelongsToCommunityAsync(request.FormDefinitionVersionId, definition.CommunityId, cancellationToken))
        {
            return Result<ProcessDefinitionVersionDto>.Failure("The start form version must belong to the process community.");
        }

        var nextVersion = (await db.ProcessDefinitionVersions
            .Where(version => version.ProcessDefinitionId == processDefinitionId)
            .MaxAsync(version => (int?)version.VersionNumber, cancellationToken) ?? 0) + 1;
        var version = new ProcessDefinitionVersion
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = definition.Id,
            ProcessDefinition = definition,
            VersionNumber = nextVersion,
            Status = DefinitionVersionStatus.Draft,
            FormDefinitionVersionId = request.FormDefinitionVersionId,
            GraphJson = JsonHelpers.Serialize(request.Graph),
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ProcessDefinitionVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "ProcessDefinitionVersion.Created",
            "ProcessDefinitionVersion",
            version.Id.ToString(),
            $"Draft version {version.VersionNumber} was created for process '{definition.Name}'.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDefinitionVersionDto>.Success(version.ToDto());
    }

    public async Task<Result<ProcessDefinitionVersionDto>> UpdateVersionAsync(
        Guid processDefinitionId,
        Guid versionId,
        UpdateProcessDefinitionVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsUpdate))
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Current user cannot update process definition versions.");
        }

        var version = await ScopedVersionQuery(user).SingleOrDefaultAsync(
            item => item.Id == versionId && item.ProcessDefinitionId == processDefinitionId,
            cancellationToken);
        if (version is null)
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Process definition version was not found.");
        }

        if (!await FormVersionBelongsToCommunityAsync(
                request.FormDefinitionVersionId,
                version.ProcessDefinition!.CommunityId,
                cancellationToken))
        {
            return Result<ProcessDefinitionVersionDto>.Failure("The start form version must belong to the process community.");
        }

        if (version.Status == DefinitionVersionStatus.Published)
        {
            return await CreateVersionAsync(
                processDefinitionId,
                new CreateProcessDefinitionVersionRequest(request.FormDefinitionVersionId, request.Graph),
                user,
                cancellationToken);
        }

        if (version.Status != DefinitionVersionStatus.Draft)
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Only draft process definition versions can be updated.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        version.FormDefinitionVersionId = request.FormDefinitionVersionId;
        version.GraphJson = JsonHelpers.Serialize(request.Graph);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "ProcessDefinitionVersion.Updated",
            "ProcessDefinitionVersion",
            version.Id.ToString(),
            $"Draft version {version.VersionNumber} of process '{version.ProcessDefinition.Name}' was updated.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDefinitionVersionDto>.Success(version.ToDto());
    }

    public async Task<Result<ProcessDefinitionVersionDto>> PublishVersionAsync(
        Guid processDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.WorkflowsPublish))
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Current user cannot publish process definition versions.");
        }

        var version = await ScopedVersionQuery(user).SingleOrDefaultAsync(
            item => item.Id == versionId && item.ProcessDefinitionId == processDefinitionId,
            cancellationToken);
        if (version is null)
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Process definition version was not found.");
        }

        if (version.Status == DefinitionVersionStatus.Published)
        {
            return Result<ProcessDefinitionVersionDto>.Success(version.ToDto());
        }

        if (version.Status != DefinitionVersionStatus.Draft)
        {
            return Result<ProcessDefinitionVersionDto>.Failure("Only draft process definition versions can be published.");
        }

        var graph = JsonHelpers.Deserialize(version.GraphJson, new ProcessGraphDto("", [], []));
        var validation = await graphValidator.ValidateForPublishAsync(
            graph,
            version.ProcessDefinition!.CommunityId,
            version.FormDefinitionVersionId,
            cancellationToken);
        if (!validation.IsSuccess)
        {
            return Result<ProcessDefinitionVersionDto>.Failure(validation.Errors);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        version.Status = DefinitionVersionStatus.Published;
        version.PublishedByUserId = user.Id;
        version.PublishedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "ProcessDefinitionVersion.Published",
            "ProcessDefinitionVersion",
            version.Id.ToString(),
            $"Version {version.VersionNumber} of process '{version.ProcessDefinition.Name}' was published.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDefinitionVersionDto>.Success(version.ToDto());
    }

    private IQueryable<ProcessDefinition> ScopedQuery(UserDto user)
    {
        var query = db.ProcessDefinitions
            .Include(definition => definition.Community)
            .Include(definition => definition.Versions)
            .AsSplitQuery();
        return user.IsSuperAdmin() ? query : query.Where(definition => definition.CommunityId == user.CommunityId);
    }

    private IQueryable<ProcessDefinitionVersion> ScopedVersionQuery(UserDto user)
    {
        var query = db.ProcessDefinitionVersions
            .Include(version => version.ProcessDefinition)
            .ThenInclude(definition => definition!.Community);
        return user.IsSuperAdmin()
            ? query
            : query.Where(version => version.ProcessDefinition != null && version.ProcessDefinition.CommunityId == user.CommunityId);
    }

    private Task<bool> FormVersionBelongsToCommunityAsync(
        Guid formVersionId,
        Guid communityId,
        CancellationToken cancellationToken) =>
        db.FormDefinitionVersions.AnyAsync(version =>
            version.Id == formVersionId
            && version.FormDefinition != null
            && version.FormDefinition.CommunityId == communityId,
            cancellationToken);
}
