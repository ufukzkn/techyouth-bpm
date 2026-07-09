using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class CommunityService(
    AppDbContext db,
    IAuthService authService,
    ISystemAuditService auditService) : ICommunityService
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
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        db.Communities.Add(community);
        await db.SaveChangesAsync(cancellationToken);
        await SeedDefaultRolesAsync(community.Id, cancellationToken);
        await auditService.LogAsync(currentUser, "Community.Created", "Community", community.Id.ToString(), $"Community '{community.Name}' was created.", cancellationToken);

        return Result<CommunityDto>.Success(community.ToDto());
    }

    public async Task<Result<CommunityDto>> UpdateAsync(Guid communityId, UpdateCommunityRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Result<CommunityDto>.Failure("Only SuperAdmin users can update communities.");
        }

        var community = await db.Communities.SingleOrDefaultAsync(item => item.Id == communityId, cancellationToken);
        if (community is null)
        {
            return Result<CommunityDto>.Failure("Community was not found.");
        }

        var errors = ValidateCommunity(request.Name);
        if (errors.Count > 0)
        {
            return Result<CommunityDto>.Failure(errors);
        }

        community.Name = request.Name.Trim();
        community.Description = request.Description.Trim();
        community.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(currentUser, "Community.Updated", "Community", community.Id.ToString(), $"Community '{community.Name}' was updated.", cancellationToken);

        return Result<CommunityDto>.Success(community.ToDto());
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

        var permissions = NormalizePermissions(request.Permissions.Count > 0 ? request.Permissions : CommunityRoleTemplates.PermissionsFor(request.TemplateKey));
        var role = new CommunityRole
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            TemplateKey = request.TemplateKey.Trim(),
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

        if (role.IsSystemRole && role.TemplateKey == CommunityRoleTemplates.CommunityAdmin)
        {
            return Result<CommunityRoleDto>.Failure("Topluluk Admin system role cannot be edited.");
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description.Trim();
        db.CommunityRolePermissions.RemoveRange(role.Permissions);
        role.Permissions.Clear();
        role.Permissions = NormalizePermissions(request.Permissions)
            .Select(permission => new CommunityRolePermission { Id = Guid.NewGuid(), CommunityRoleId = role.Id, Permission = permission })
            .ToList();

        var errors = await ValidateRoleAsync(role, cancellationToken, role.Id);
        if (errors.Count > 0)
        {
            return Result<CommunityRoleDto>.Failure(errors);
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(currentUser, "CommunityRole.Updated", "CommunityRole", role.Id.ToString(), $"Community role '{role.Name}' was updated.", cancellationToken);

        return Result<CommunityRoleDto>.Success(role.ToDto());
    }

    public async Task<Result<PagedResult<UserAdminDto>>> ListUsersAsync(Guid communityId, UserDto currentUser, UserSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageUsers))
        {
            return Result<PagedResult<UserAdminDto>>.Failure("Current user cannot view community users.");
        }

        return await authService.ListUsersAsync(currentUser, request with { CommunityId = communityId }, cancellationToken);
    }

    public async Task<Result<UserAdminDto>> CreateUserAsync(Guid communityId, CreateUserRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageUsers))
        {
            return Result<UserAdminDto>.Failure("Current user cannot create community users.");
        }

        return await authService.CreateUserAsync(request with { CommunityId = communityId }, currentUser, cancellationToken);
    }

    public async Task<Result<UserAdminDto>> UpdateMembershipAsync(Guid communityId, Guid userId, UpdateUserMembershipRequest request, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        if (!CanManageCommunity(currentUser, communityId, PermissionNames.CommunityManageUsers))
        {
            return Result<UserAdminDto>.Failure("Current user cannot update community memberships.");
        }

        var user = await db.Users
            .Include(item => item.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(item => item.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<UserAdminDto>.Failure("User not found.");
        }

        var roleExists = await db.CommunityRoles.AnyAsync(role => role.Id == request.CommunityRoleId && role.CommunityId == communityId, cancellationToken);
        if (!roleExists)
        {
            return Result<UserAdminDto>.Failure("Community role was not found.");
        }

        foreach (var membership in user.CommunityMemberships)
        {
            membership.IsActive = false;
        }

        user.CommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CommunityId = communityId,
            CommunityRoleId = request.CommunityRoleId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(currentUser, "CommunityMembership.Updated", "User", user.Id.ToString(), $"User '{user.Username}' membership was updated.", cancellationToken);

        return Result<UserAdminDto>.Success(user.ToAdminDto());
    }

    private async Task SeedDefaultRolesAsync(Guid communityId, CancellationToken cancellationToken)
    {
        foreach (var template in CommunityRoleTemplates.All)
        {
            if (await db.CommunityRoles.AnyAsync(role => role.CommunityId == communityId && role.TemplateKey == template.Key, cancellationToken))
            {
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

    private async Task<List<string>> ValidateRoleAsync(CommunityRole role, CancellationToken cancellationToken, Guid? existingRoleId = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(role.Name))
        {
            errors.Add("Community role name is required.");
        }

        if (role.Permissions.Count == 0)
        {
            errors.Add("At least one permission is required.");
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
