using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class CommunityService(
    AppDbContext db,
    ISystemAuditService auditService) : ICommunityService, ICommunityRoleService
{
    public async Task<Result<IReadOnlyList<CommunityDto>>> ListAsync(UserDto currentUser, CancellationToken cancellationToken = default)
    {
        var query = db.Communities.AsNoTracking();
        if (!currentUser.IsSuperAdmin())
        {
            if (currentUser.CommunityId is null)
            {
                return Result<IReadOnlyList<CommunityDto>>.Success([]);
            }

            query = query.Where(community => community.Id == currentUser.CommunityId);
        }

        var communities = await query.OrderBy(community => community.Name).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CommunityDto>>.Success(communities.Select(community => community.ToDto()).ToArray());
    }

    public async Task<Result<CommunityDto>> CreateAsync(CreateCommunityRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<CommunityDto>.Failure("Only SuperAdmin users can create communities.");
        }

        var errors = ValidateCommunity(request.Name);
        var inviteCode = await ResolveInviteCodeAsync(request.InviteCode, null, cancellationToken);
        if (inviteCode is null)
        {
            errors.Add("Community code must be five uppercase letters or digits and unique.");
        }
        if (errors.Count > 0)
        {
            return Result<CommunityDto>.Failure(errors);
        }

        var name = request.Name.Trim();
        if (await db.Communities.AnyAsync(community => community.Name == name, cancellationToken))
        {
            return Result<CommunityDto>.Failure("Community name is already used.");
        }

        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description.Trim(),
            InviteCode = inviteCode ?? string.Empty,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        db.Communities.Add(community);
        await db.SaveChangesAsync(cancellationToken);
        await SeedDefaultRolesAsync(community.Id, cancellationToken);
        await auditService.LogAsync(currentUser, "Community.Created", "Community", community.Id.ToString(), $"Community '{community.Name}' was created.", cancellationToken);

        return Result<CommunityDto>.Success(community.ToDto());
    }

    public async Task<Result<CommunityDto>> RegenerateInviteCodeAsync(Guid communityId, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<CommunityDto>.Failure("Only SuperAdmin users can regenerate community codes.");
        }

        var community = await db.Communities.SingleOrDefaultAsync(item => item.Id == communityId, cancellationToken);
        if (community is null)
        {
            return Result<CommunityDto>.Failure("Community was not found.");
        }

        community.InviteCode = await GenerateUniqueInviteCodeAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(currentUser, "Community.InviteCodeRegenerated", "Community", community.Id.ToString(), $"Community '{community.Name}' invite code was regenerated.", cancellationToken);

        return Result<CommunityDto>.Success(community.ToDto());
    }

    public async Task<Result<CommunityDto>> UpdateAsync(Guid communityId, UpdateCommunityRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        var community = await db.Communities.SingleOrDefaultAsync(item => item.Id == communityId, cancellationToken);
        if (community is null)
        {
            return Result<CommunityDto>.Failure("Community was not found.");
        }

        var isCommunityAdminStatusUpdate = !currentUser.IsSuperAdmin()
            && currentUser.CommunityId == communityId
            && currentUser.HasPermission(PermissionNames.CommunityManageAdmins)
            && string.Equals(community.Name, request.Name.Trim(), StringComparison.Ordinal)
            && string.Equals(community.Description, request.Description.Trim(), StringComparison.Ordinal)
            && string.Equals(community.InviteCode, request.InviteCode?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!currentUser.IsSuperAdmin() && !isCommunityAdminStatusUpdate)
        {
            return Result<CommunityDto>.Failure("Only SuperAdmin users can update community settings.");
        }

        var errors = isCommunityAdminStatusUpdate ? [] : ValidateCommunity(request.Name);
        var inviteCode = isCommunityAdminStatusUpdate
            ? community.InviteCode
            : await ResolveInviteCodeAsync(request.InviteCode, communityId, cancellationToken);
        if (inviteCode is null)
        {
            errors.Add("Community code must be five uppercase letters or digits and unique.");
        }
        if (errors.Count > 0)
        {
            return Result<CommunityDto>.Failure(errors);
        }

        var duplicateName = !isCommunityAdminStatusUpdate && await db.Communities.AnyAsync(
            item => item.Id != communityId && item.Name == request.Name.Trim(),
            cancellationToken);
        if (duplicateName)
        {
            return Result<CommunityDto>.Failure("Community name is already used.");
        }

        var wasActive = community.IsActive;
        community.Name = request.Name.Trim();
        community.Description = request.Description.Trim();
        community.InviteCode = inviteCode!;
        community.IsActive = request.IsActive;

        if (wasActive && !community.IsActive)
        {
            var memberIds = await db.UserCommunityMemberships
                .Where(membership => membership.CommunityId == communityId
                    && membership.IsActive
                    && membership.UserId != currentUser.Id)
                .Select(membership => membership.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (memberIds.Count > 0)
            {
                var activeSessions = await db.UserSessions
                    .Where(session => memberIds.Contains(session.UserId) && session.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var session in activeSessions)
                {
                    session.RevokedAt = DateTime.UtcNow;
                }

                var refreshTokens = await db.RefreshTokens
                    .Where(refreshToken => memberIds.Contains(refreshToken.UserId) && refreshToken.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var refreshToken in refreshTokens)
                {
                    refreshToken.RevokedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            community.IsActive
                ? (wasActive ? "Community.Updated" : "Community.Reactivated")
                : "Community.Deactivated",
            "Community",
            community.Id.ToString(),
            community.IsActive
                ? (wasActive
                    ? $"Community '{community.Name}' was updated."
                    : $"Community '{community.Name}' was reactivated.")
                : $"Community '{community.Name}' was deactivated and active member sessions were revoked.",
            cancellationToken);

        return Result<CommunityDto>.Success(community.ToDto());
    }

    public async Task<Result<CommunitySummaryDto>> GetSummaryAsync(Guid communityId, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageRoles)
            && !CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageUsers))
        {
            return Result<CommunitySummaryDto>.Failure("Current user cannot view community summary.");
        }

        var roleCounts = await db.CommunityRoles.AsNoTracking()
            .Where(role => role.CommunityId == communityId)
            .OrderBy(role => role.Name)
            .Select(role => new CommunityRoleCountDto(
                role.Id,
                role.Name,
                db.UserCommunityMemberships.Count(membership => membership.CommunityRoleId == role.Id && membership.IsActive)))
            .ToListAsync(cancellationToken);
        var memberCount = await db.UserCommunityMemberships.CountAsync(
            membership => membership.CommunityId == communityId && membership.IsActive,
            cancellationToken);

        return Result<CommunitySummaryDto>.Success(new CommunitySummaryDto(communityId, memberCount, roleCounts));
    }

    public Task<Result<IReadOnlyList<RoleTemplateDto>>> ListRoleTemplatesAsync(UserDto currentUser, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(currentUser.IsSuperAdmin() || currentUser.HasPermission(PermissionNames.CommunityManageRoles)
            ? Result<IReadOnlyList<RoleTemplateDto>>.Success(CommunityRoleTemplates.All)
            : Result<IReadOnlyList<RoleTemplateDto>>.Failure("Current user cannot view role templates."));
    }

    public async Task<Result<IReadOnlyList<CommunityRoleDto>>> ListRolesAsync(Guid communityId, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageRoles))
        {
            return Result<IReadOnlyList<CommunityRoleDto>>.Failure("Current user cannot view community roles.");
        }

        var roles = await RoleQuery()
            .Where(role => role.CommunityId == communityId)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CommunityRoleDto>>.Success(roles.Select(role => role.ToDto()).ToArray());
    }

    public async Task<Result<CommunityRoleDto>> CreateRoleAsync(Guid communityId, CreateCommunityRoleRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageRoles))
        {
            return Result<CommunityRoleDto>.Failure("Current user cannot create community roles.");
        }

        var isReadyTemplate = CommunityRoleTemplates.All.Any(template =>
            template.Key != CommunityRoleTemplates.Custom
            && template.Key.Equals(request.TemplateKey.Trim(), StringComparison.OrdinalIgnoreCase));
        var permissions = NormalizePermissions(request.Permissions.Count > 0 ? request.Permissions : CommunityRoleTemplates.PermissionsFor(request.TemplateKey));
        var role = new CommunityRole
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            TemplateKey = isReadyTemplate ? CommunityRoleTemplates.Custom : request.TemplateKey.Trim(),
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow,
            Permissions = permissions.Select(permission => new CommunityRolePermission
            {
                Id = Guid.NewGuid(),
                Permission = permission
            }).ToList()
        };

        var errors = await ValidateRoleAsync(role, cancellationToken);
        if (errors.Count > 0)
        {
            return Result<CommunityRoleDto>.Failure(errors);
        }

        db.CommunityRoles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(currentUser, "CommunityRole.Created", "CommunityRole", role.Id.ToString(), $"Community role '{role.Name}' was created.", cancellationToken);

        return Result<CommunityRoleDto>.Success(role.ToDto());
    }

    public async Task<Result<CommunityRoleDto>> UpdateRoleAsync(Guid communityId, Guid roleId, UpdateCommunityRoleRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageRoles))
        {
            return Result<CommunityRoleDto>.Failure("Current user cannot update community roles.");
        }

        var role = await RoleQuery().SingleOrDefaultAsync(item => item.Id == roleId && item.CommunityId == communityId, cancellationToken);
        if (role is null)
        {
            return Result<CommunityRoleDto>.Failure("Community role was not found.");
        }

        if (role.IsSystemRole)
        {
            return Result<CommunityRoleDto>.Failure("System community roles cannot be edited.");
        }

        var previousName = role.Name;
        var previousDescription = role.Description;
        var previousPermissions = role.Permissions.Select(item => item.Permission).Order().ToArray();
        var requestedPermissions = NormalizePermissions(request.Permissions);
        role.Name = request.Name.Trim();
        role.Description = request.Description.Trim();

        var errors = await ValidateRoleAsync(role, cancellationToken, role.Id);
        if (errors.Count > 0)
        {
            return Result<CommunityRoleDto>.Failure(errors);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        SynchronizePermissions(role, requestedPermissions);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "CommunityRole.Updated",
            "CommunityRole",
            role.Id.ToString(),
            $"Community role '{role.Name}' was updated.",
            new SystemAuditContext(
                CommunityId: communityId,
                Metadata: new
                {
                    before = new { name = previousName, description = previousDescription, permissions = previousPermissions },
                    after = new { name = role.Name, description = role.Description, permissions = requestedPermissions }
                }),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<CommunityRoleDto>.Success(role.ToDto());
    }

    public async Task<Result> DeleteRoleAsync(
        Guid communityId,
        Guid roleId,
        DeleteCommunityRoleRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageRoles))
        {
            return Result.Failure("Current user cannot delete community roles.");
        }

        var role = await RoleQuery().SingleOrDefaultAsync(item => item.Id == roleId && item.CommunityId == communityId, cancellationToken);
        if (role is null)
        {
            return Result.Failure("Community role was not found.");
        }

        if (role.IsSystemRole)
        {
            return Result.Failure("System community roles cannot be deleted.");
        }

        if (request.ReplacementRoleId == roleId)
        {
            return Result.Failure("A role cannot replace itself.");
        }

        var replacementRole = await RoleQuery().SingleOrDefaultAsync(
            item => item.Id == request.ReplacementRoleId && item.CommunityId == communityId,
            cancellationToken);
        if (replacementRole is null)
        {
            return Result.Failure("Replacement community role was not found.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var memberships = await db.UserCommunityMemberships
            .Where(membership => membership.CommunityRoleId == roleId && membership.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            membership.CommunityRoleId = replacementRole.Id;
        }

        db.CommunityRoles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            currentUser,
            "CommunityRole.Deleted",
            "CommunityRole",
            roleId.ToString(),
            $"Community role '{role.Name}' was deleted; {memberships.Count} active memberships were moved to '{replacementRole.Name}'.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task SeedDefaultRolesAsync(Guid communityId, CancellationToken cancellationToken)
    {
        foreach (var template in CommunityRoleTemplates.All)
        {
            if (template.Key == CommunityRoleTemplates.Custom)
            {
                continue;
            }

            var existingRole = await RoleQuery().SingleOrDefaultAsync(
                role => role.CommunityId == communityId && role.TemplateKey == template.Key,
                cancellationToken);
            if (existingRole is not null)
            {
                if (existingRole.IsSystemRole)
                {
                    existingRole.Name = template.Name;
                    existingRole.Description = template.Description;
                    existingRole.Permissions.Clear();
                    existingRole.Permissions.AddRange(template.Permissions.Select(permission => new CommunityRolePermission
                    {
                        Id = Guid.NewGuid(),
                        CommunityRoleId = existingRole.Id,
                        Permission = permission
                    }));
                }
                continue;
            }

            db.CommunityRoles.Add(new CommunityRole
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                Name = template.Name,
                Description = template.Description,
                TemplateKey = template.Key,
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow,
                Permissions = template.Permissions.Select(permission => new CommunityRolePermission
                {
                    Id = Guid.NewGuid(),
                    Permission = permission
                }).ToList()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private bool CanManageCommunity(UserDto user, Guid communityId, string permission)
    {
        return user.IsSuperAdmin()
            || (user.CommunityId == communityId && user.HasPermission(permission));
    }

    private IQueryable<CommunityRole> RoleQuery() =>
        db.CommunityRoles.Include(role => role.Permissions);

    private static List<string> NormalizePermissions(IEnumerable<string> permissions) =>
        permissions
            .Where(permission => PermissionNames.All.Contains(permission, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();

    private void SynchronizePermissions(CommunityRole role, IReadOnlyCollection<string> requestedPermissions)
    {
        var requested = requestedPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = role.Permissions
            .Where(permission => !requested.Contains(permission.Permission))
            .ToArray();
        if (removed.Length > 0)
        {
            role.Permissions.RemoveAll(permission => removed.Contains(permission));
        }

        var existing = role.Permissions
            .Select(permission => permission.Permission)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var permission in requested.Where(permission => !existing.Contains(permission)))
        {
            var addedPermission = new CommunityRolePermission
            {
                Id = Guid.NewGuid(),
                CommunityRoleId = role.Id,
                Permission = permission
            };
            role.Permissions.Add(addedPermission);
            db.CommunityRolePermissions.Add(addedPermission);
        }
    }

    private async Task<List<string>> ValidateRoleAsync(CommunityRole role, CancellationToken cancellationToken, Guid? existingRoleId = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(role.Name))
        {
            errors.Add("Community role name is required.");
        }

        var exists = await db.CommunityRoles.AnyAsync(
            item => item.CommunityId == role.CommunityId
                && item.Name == role.Name
                && item.Id != existingRoleId,
            cancellationToken);
        if (exists)
        {
            errors.Add("Community role name is already used.");
        }

        return errors;
    }

    private async Task<string> GenerateUniqueInviteCodeAsync(CancellationToken cancellationToken)
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        }
        while (await db.Communities.AnyAsync(community => community.InviteCode == code, cancellationToken));

        return code;
    }

    private async Task<string?> ResolveInviteCodeAsync(string? requestedCode, Guid? currentCommunityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedCode))
        {
            return currentCommunityId is null
                ? await GenerateUniqueInviteCodeAsync(cancellationToken)
                : await db.Communities
                    .Where(community => community.Id == currentCommunityId)
                    .Select(community => community.InviteCode)
                    .SingleOrDefaultAsync(cancellationToken);
        }

        var normalized = requestedCode.Trim().ToUpperInvariant();
        if (normalized.Length != 5 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            return null;
        }

        var alreadyUsed = await db.Communities.AnyAsync(
            community => community.Id != currentCommunityId && community.InviteCode == normalized,
            cancellationToken);
        return alreadyUsed ? null : normalized;
    }

    private static List<string> ValidateCommunity(string name)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Community name is required.");
        }

        return errors;
    }
}
