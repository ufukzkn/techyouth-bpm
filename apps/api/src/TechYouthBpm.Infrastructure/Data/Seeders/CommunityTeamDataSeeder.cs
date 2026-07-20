using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Security;
using TechYouthBpm.Infrastructure.Services;
using static TechYouthBpm.Infrastructure.Data.Seeders.DemoSeedIds;

namespace TechYouthBpm.Infrastructure.Data.Seeders;

internal static class CommunityTeamDataSeeder
{
    public static Task SeedCommunitiesAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default) =>
        SeedCommunityRecordsAsync(db, cancellationToken);

    public static Task SeedTeamsAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default) =>
        SeedTeamRecordsAsync(db, cancellationToken);

    private static async Task SeedCommunityRecordsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var communitySeeds = new[]
        {
            Community(SportCommunityId, "Sportif Faaliyetler", "Transfer, teknik ekip onayi ve sportif operasyon surecleri."),
            Community(LogisticsCommunityId, "Lojistik", "Kargo, sevkiyat ve teslimat operasyon surecleri."),
            Community(ProductOrderCommunityId, "Urun Siparisi", "Siparis talebi, stok kontrolu ve onay surecleri."),
            Community(HumanResourcesCommunityId, "Insan Kaynaklari", "Izin, uzaktan calisma ve ekip kapasitesi surecleri."),
            Community(ProcurementCommunityId, "Satin Alma", "Tedarikci, butce ve satin alma talep surecleri.")
        };
        var existingCommunities = await db.Communities.ToListAsync(cancellationToken);
        var existingCommunityIds = existingCommunities.Select(community => community.Id).ToHashSet();
        var missingCommunities = communitySeeds.Where(community => !existingCommunityIds.Contains(community.Id)).ToArray();
        if (missingCommunities.Length > 0)
        {
            db.Communities.AddRange(missingCommunities);
        }

        foreach (var community in existingCommunities)
        {
            var expectedInviteCode = community.Name switch
            {
                "Sportif Faaliyetler" => "SPOR1",
                "Lojistik" => "LOG01",
                "Urun Siparisi" => "URUN1",
                "Insan Kaynaklari" => "IK001",
                "Satin Alma" => "SAT01",
                _ => string.IsNullOrWhiteSpace(community.InviteCode) ? GenerateInviteCode() : community.InviteCode
            };
            community.InviteCode = expectedInviteCode;
        }

        await db.SaveChangesAsync(cancellationToken);

        var roleSeeds = BuildCommunityRoles();
        var existingRoles = await db.CommunityRoles
            .Select(role => new { role.Id, role.CommunityId, role.Name })
            .ToListAsync(cancellationToken);
        var existingRoleIdSet = existingRoles.Select(role => role.Id).ToHashSet();
        var existingRoleNameSet = existingRoles
            .Select(role => (role.CommunityId, role.Name))
            .ToHashSet();
        db.CommunityRoles.AddRange(roleSeeds.Where(role =>
            !existingRoleIdSet.Contains(role.Id)
            && !existingRoleNameSet.Contains((role.CommunityId, role.Name))));
        await db.SaveChangesAsync(cancellationToken);
        await EnsureSystemRoleTemplatesAsync(db, cancellationToken);
        await RetireDuplicateLogisticsRoleAsync(db, cancellationToken);
    }

    private static async Task SeedTeamRecordsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var teamSeeds = BuildTeamSeeds();
        var existingTeams = await db.Teams.ToListAsync(cancellationToken);
        var existingTeamIds = existingTeams.Select(team => team.Id).ToHashSet();
        var existingNames = existingTeams
            .Select(team => (team.CommunityId, team.NormalizedName))
            .ToHashSet();

        var missingTeams = teamSeeds
            .Where(team => !existingTeamIds.Contains(team.Id)
                && !existingNames.Contains((team.CommunityId, team.NormalizedName)))
            .ToArray();
        if (missingTeams.Length > 0)
        {
            db.Teams.AddRange(missingTeams);
            await db.SaveChangesAsync(cancellationToken);
        }

        var teamIds = teamSeeds.Select(team => team.Id).ToArray();
        var availableTeamIds = await db.Teams
            .Where(team => teamIds.Contains(team.Id))
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);
        var existingMemberships = await db.TeamMemberships
            .Where(membership => availableTeamIds.Contains(membership.TeamId))
            .Select(membership => new { membership.TeamId, membership.UserId })
            .ToListAsync(cancellationToken);
        var existingMembershipKeys = existingMemberships
            .Select(membership => (membership.TeamId, membership.UserId))
            .ToHashSet();
        var availableUserIds = (await db.Users
            .Select(user => user.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var missingMemberships = BuildTeamMembershipSeeds()
            .Where(membership => availableTeamIds.Contains(membership.TeamId)
                && availableUserIds.Contains(membership.UserId)
                && !existingMembershipKeys.Contains((membership.TeamId, membership.UserId)))
            .ToArray();
        if (missingMemberships.Length > 0)
        {
            db.TeamMemberships.AddRange(missingMemberships);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<Team> BuildTeamSeeds() =>
    [
        TeamSeed(SportScoutTeamId, SportCommunityId, "Scout Ekibi", "Oyuncu izleme ve ilk teknik raporlama.", FatihTerimId),
        TeamSeed(SportTechnicalTeamId, SportCommunityId, "Teknik Degerlendirme", "Teknik uygunluk ve kadro degerlendirmesi.", FatihTerimId),
        TeamSeed(SportFinanceTeamId, SportCommunityId, "Mali Isler", "Transfer butcesi ve mali uygunluk kontrolu.", FatihTerimId),
        TeamSeed(SportTransferTeamId, SportCommunityId, "Transfer Operasyon", "Sozlesme ve transfer operasyon takibi.", FatihTerimId),
        TeamSeed(LogisticsPlanningTeamId, LogisticsCommunityId, "Sevkiyat Planlama", "Sevkiyat rotasi ve kapasite planlamasi.", AtibaId),
        TeamSeed(LogisticsWarehouseTeamId, LogisticsCommunityId, "Depo Operasyon", "Depo cikis ve stok hareketleri.", AtibaId),
        TeamSeed(LogisticsDeliveryTeamId, LogisticsCommunityId, "Teslimat Takibi", "Teslimat durumu ve hedefe ulasim takibi.", AtibaId),
        TeamSeed(ProductIntakeTeamId, ProductOrderCommunityId, "Siparis Kabul", "Yeni urun siparislerinin ilk kontrolu.", AlexId),
        TeamSeed(ProductStockTeamId, ProductOrderCommunityId, "Stok Kontrol", "Stok ve urun uygunlugu kontrolu.", AlexId),
        TeamSeed(ProductFulfillmentTeamId, ProductOrderCommunityId, "Siparis Hazirlama", "Onaylanan siparislerin hazirlanmasi.", AlexId),
        TeamSeed(HrTalentTeamId, HumanResourcesCommunityId, "Yetenek Kazanimi", "Aday ve ise alim operasyonlari.", SenolGunesId),
        TeamSeed(HrExperienceTeamId, HumanResourcesCommunityId, "Calisan Deneyimi", "Izin ve calisan deneyimi talepleri.", SenolGunesId),
        TeamSeed(HrPayrollTeamId, HumanResourcesCommunityId, "Bordro ve Ozluk", "Bordro ve ozluk kontrol surecleri.", SenolGunesId),
        TeamSeed(ProcurementRequestTeamId, ProcurementCommunityId, "Talep Degerlendirme", "Satin alma taleplerinin ilk degerlendirmesi.", AliKocId),
        TeamSeed(ProcurementVendorTeamId, ProcurementCommunityId, "Tedarikci Yonetimi", "Tedarikci secimi ve teklif karsilastirmasi.", AliKocId),
        TeamSeed(ProcurementBudgetTeamId, ProcurementCommunityId, "Butce Kontrol", "Butce uygunlugu ve harcama kontrolu.", AliKocId)
    ];

    private static IReadOnlyList<TeamMembership> BuildTeamMembershipSeeds() =>
    [
        TeamMember(SportScoutTeamId, UserId, true),
        TeamMember(SportScoutTeamId, ZlatanIbrahimovicId),
        TeamMember(SportScoutTeamId, QuaresmaId),
        TeamMember(SportTechnicalTeamId, ApproverId, true),
        TeamMember(SportTechnicalTeamId, QuaresmaId),
        TeamMember(SportFinanceTeamId, OkanBurukId, true),
        TeamMember(SportFinanceTeamId, QuaresmaId),
        TeamMember(SportFinanceTeamId, FatihTerimId, true),
        TeamMember(SportTransferTeamId, FatihTerimId, true),
        TeamMember(SportTransferTeamId, QuaresmaId),
        TeamMember(LogisticsPlanningTeamId, JoseMourinhoId, true),
        TeamMember(LogisticsPlanningTeamId, EmreBelozogluId),
        TeamMember(LogisticsWarehouseTeamId, AtibaId, true),
        TeamMember(LogisticsWarehouseTeamId, DirkKuytId),
        TeamMember(LogisticsDeliveryTeamId, SergenYalcinId, true),
        TeamMember(LogisticsDeliveryTeamId, JoseMourinhoId),
        TeamMember(ProductIntakeTeamId, TaliscaId, true),
        TeamMember(ProductIntakeTeamId, CanerErkinId),
        TeamMember(ProductStockTeamId, FerdiKadiogluId, true),
        TeamMember(ProductStockTeamId, WesleySneijderId),
        TeamMember(ProductFulfillmentTeamId, AlexId, true),
        TeamMember(ProductFulfillmentTeamId, FerdiKadiogluId),
        TeamMember(HrTalentTeamId, SenolGunesId, true),
        TeamMember(HrTalentTeamId, ArdaTuranId),
        TeamMember(HrExperienceTeamId, ArdaGulerId, true),
        TeamMember(HrExperienceTeamId, IlhanMansizId),
        TeamMember(HrPayrollTeamId, BurakYilmazId, true),
        TeamMember(HrPayrollTeamId, OguzhanOzyakupId),
        TeamMember(HrPayrollTeamId, SenolGunesId),
        TeamMember(ProcurementRequestTeamId, AliKocId, true),
        TeamMember(ProcurementRequestTeamId, DembaBaId),
        TeamMember(ProcurementVendorTeamId, NecipUysalId, true),
        TeamMember(ProcurementVendorTeamId, NaniId),
        TeamMember(ProcurementBudgetTeamId, RobinGosensId, true),
        TeamMember(ProcurementBudgetTeamId, AliKocId)
    ];

    private static Team TeamSeed(
        Guid id,
        Guid communityId,
        string name,
        string description,
        Guid createdByUserId) =>
        new()
        {
            Id = id,
            CommunityId = communityId,
            Name = name,
            NormalizedName = name.Trim().ToUpperInvariant(),
            Description = description,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-18),
            UpdatedAt = DateTime.UtcNow.AddDays(-18)
        };

    private static TeamMembership TeamMember(Guid teamId, Guid userId, bool isLead = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            IsLead = isLead,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            UpdatedAt = DateTime.UtcNow.AddDays(-15)
        };

    private static Community Community(Guid id, string name, string description) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            InviteCode = name switch
            {
                "Sportif Faaliyetler" => "SPOR1",
                "Lojistik" => "LOG01",
                "Urun Siparisi" => "URUN1",
                "Insan Kaynaklari" => "IK001",
                "Satin Alma" => "SAT01",
                _ => "GEN01"
            },
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-40)
        };

    private static IReadOnlyList<CommunityRole> BuildCommunityRoles() =>
    [
        RoleSeed(SportAdminRoleId, SportCommunityId, "Topluluk Admin", "Sportif faaliyetler toplulugunu yonetir.", "community-admin", PermissionNames.All),
        RoleSeed(SportUnassignedRoleId, SportCommunityId, "Atanmadi", "Onay bekleyen veya henuz yetki verilmeyen kullanici rolu.", "unassigned", []),
        RoleSeed(SportStarterRoleId, SportCommunityId, "Surec Baslatici", "Transfer talep sureclerini baslatir.", "process-starter", [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]),
        RoleSeed(SportApproverRoleId, SportCommunityId, "Onay Sorumlusu", "Sportif tasklari onaylar veya reddeder.", "approver", [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        RoleSeed(LogisticsAdminRoleId, LogisticsCommunityId, "Topluluk Admin", "Lojistik toplulugunu yonetir.", "community-admin", PermissionNames.All),
        RoleSeed(LogisticsUnassignedRoleId, LogisticsCommunityId, "Atanmadi", "Onay bekleyen veya henuz yetki verilmeyen kullanici rolu.", "unassigned", []),
        RoleSeed(LogisticsStarterRoleId, LogisticsCommunityId, "Surec Baslatici", "Lojistik taleplerini baslatir.", "process-starter", [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]),
        RoleSeed(LogisticsApproverRoleId, LogisticsCommunityId, "Onay Sorumlusu", "Lojistik tasklarini onaylar veya reddeder.", "approver", [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        RoleSeed(ProductAdminRoleId, ProductOrderCommunityId, "Topluluk Admin", "Urun siparisi toplulugunu yonetir.", "community-admin", PermissionNames.All),
        RoleSeed(ProductUnassignedRoleId, ProductOrderCommunityId, "Atanmadi", "Onay bekleyen veya henuz yetki verilmeyen kullanici rolu.", "unassigned", []),
        RoleSeed(ProductStarterRoleId, ProductOrderCommunityId, "Surec Baslatici", "Urun siparisi taleplerini baslatir.", "process-starter", [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]),
        RoleSeed(ProductApproverRoleId, ProductOrderCommunityId, "Onay Sorumlusu", "Urun siparisi tasklarini onaylar veya reddeder.", "approver", [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        RoleSeed(HumanResourcesAdminRoleId, HumanResourcesCommunityId, "Topluluk Admin", "Insan kaynaklari toplulugunu yonetir.", "community-admin", PermissionNames.All),
        RoleSeed(HumanResourcesUnassignedRoleId, HumanResourcesCommunityId, "Atanmadi", "Onay bekleyen veya henuz yetki verilmeyen kullanici rolu.", "unassigned", []),
        RoleSeed(HumanResourcesStarterRoleId, HumanResourcesCommunityId, "Surec Baslatici", "Izin ve calisma duzeni taleplerini baslatir.", "process-starter", [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]),
        RoleSeed(HumanResourcesApproverRoleId, HumanResourcesCommunityId, "Onay Sorumlusu", "Insan kaynaklari tasklarini onaylar veya reddeder.", "approver", [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct]),
        RoleSeed(ProcurementAdminRoleId, ProcurementCommunityId, "Topluluk Admin", "Satin alma toplulugunu yonetir.", "community-admin", PermissionNames.All),
        RoleSeed(ProcurementUnassignedRoleId, ProcurementCommunityId, "Atanmadi", "Onay bekleyen veya henuz yetki verilmeyen kullanici rolu.", "unassigned", []),
        RoleSeed(ProcurementStarterRoleId, ProcurementCommunityId, "Surec Baslatici", "Satin alma taleplerini baslatir.", "process-starter", [PermissionNames.FormsView, PermissionNames.ProcessesView, PermissionNames.ProcessesStart]),
        RoleSeed(ProcurementApproverRoleId, ProcurementCommunityId, "Onay Sorumlusu", "Satin alma tasklarini onaylar veya reddeder.", "approver", [PermissionNames.ProcessesView, PermissionNames.TasksView, PermissionNames.TasksAct])
    ];

    private static async Task EnsureSystemRoleTemplatesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var communityIds = await db.Communities.Select(community => community.Id).ToListAsync(cancellationToken);
        var roles = await db.CommunityRoles
            .Include(role => role.Permissions)
            .ToListAsync(cancellationToken);
        var permissionsToReplace = new List<(Guid RoleId, IReadOnlyList<string> Permissions)>();

        foreach (var communityId in communityIds)
        {
            foreach (var template in CommunityRoleTemplates.All.Where(template => template.Key != CommunityRoleTemplates.Custom))
            {
                var role = roles.SingleOrDefault(item => item.CommunityId == communityId && item.TemplateKey == template.Key)
                    ?? roles.SingleOrDefault(item => item.CommunityId == communityId && item.IsSystemRole && item.Name == template.Name);
                if (role is null)
                {
                    role = RoleSeed(Guid.NewGuid(), communityId, template.Name, template.Description, template.Key, []);
                    db.CommunityRoles.Add(role);
                    roles.Add(role);
                }
                else if (!role.IsSystemRole)
                {
                    continue;
                }

                role.Name = template.Name;
                role.Description = template.Description;
                db.CommunityRolePermissions.RemoveRange(role.Permissions);
                permissionsToReplace.Add((role.Id, template.Permissions));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (roleId, permissions) in permissionsToReplace)
        {
            db.CommunityRolePermissions.AddRange(permissions.Select(permission => new CommunityRolePermission
            {
                Id = Guid.NewGuid(),
                CommunityRoleId = roleId,
                Permission = permission
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task RetireDuplicateLogisticsRoleAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var legacyRoles = await db.CommunityRoles
            .Where(role => role.IsSystemRole && role.TemplateKey == "logistics-operator")
            .ToListAsync(cancellationToken);

        foreach (var legacyRole in legacyRoles)
        {
            var replacementRole = await db.CommunityRoles.SingleOrDefaultAsync(
                role => role.CommunityId == legacyRole.CommunityId && role.TemplateKey == CommunityRoleTemplates.Approver,
                cancellationToken);
            if (replacementRole is null)
            {
                legacyRole.Name = "Onay Sorumlusu";
                legacyRole.Description = "Topluluktaki tasklari onaylar veya reddeder.";
                legacyRole.TemplateKey = CommunityRoleTemplates.Approver;
                continue;
            }

            var memberships = await db.UserCommunityMemberships
                .Where(membership => membership.CommunityRoleId == legacyRole.Id)
                .ToListAsync(cancellationToken);
            foreach (var membership in memberships)
            {
                membership.CommunityRoleId = replacementRole.Id;
            }

            db.CommunityRoles.Remove(legacyRole);
        }

        if (legacyRoles.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static CommunityRole RoleSeed(
        Guid id,
        Guid communityId,
        string name,
        string description,
        string templateKey,
        IReadOnlyList<string> permissions) =>
        new()
        {
            Id = id,
            CommunityId = communityId,
            Name = name,
            Description = description,
            TemplateKey = templateKey,
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow.AddDays(-35),
            Permissions = permissions.Select(permission => new CommunityRolePermission
            {
                Id = Guid.NewGuid(),
                Permission = permission
            }).ToList()
        };

    private static string GenerateInviteCode() =>
        Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
}
