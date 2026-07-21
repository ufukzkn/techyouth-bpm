using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Security;

namespace TechYouthBpm.Infrastructure.Services;

internal sealed class AuthenticatedUserLoader(AppDbContext db)
{
    public async Task<UserDto?> LoadAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = SessionTokenHasher.Hash(token);
        var session = await db.UserSessions
            .AsSplitQuery()
            .Include(item => item.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.Community)
            .Include(item => item.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .Include(item => item.User)
            .ThenInclude(user => user!.TeamMemberships)
            .ThenInclude(teamMembership => teamMembership.Team)
            .SingleOrDefaultAsync(
                item => item.Token == tokenHash && item.ExpiresAt > DateTime.UtcNow && item.RevokedAt == null,
                cancellationToken);

        if (session?.User is null
            || session.User.Status != UserStatus.Active
            || !AuthAccessRules.HasActiveCommunityAccess(session.User))
        {
            if (session is not null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        if (session.LastSeenAt is null || (DateTime.UtcNow - session.LastSeenAt.Value).TotalMinutes >= 5)
        {
            session.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return session.User.ToDto();
    }
}

internal static class AuthAccessRules
{
    public static bool HasActiveCommunityAccess(User user) =>
        user.Role == Role.SuperAdmin
        || user.CommunityMemberships.Count == 0
        || user.CommunityMemberships.Any(membership =>
            membership.IsActive
            && membership.CommunityRole is not null
            && (membership.Community?.IsActive == true
                || membership.CommunityRole.Permissions.Any(permission =>
                    permission.Permission == PermissionNames.CommunityManageAdmins)));
}
