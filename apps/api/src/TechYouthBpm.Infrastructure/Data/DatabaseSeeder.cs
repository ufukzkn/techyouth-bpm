using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Security;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Infrastructure.Data;

public static class DatabaseSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ApproverId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MarioGomezId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid QuaresmaId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid AtibaId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid AlexId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid FatihTerimId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid SergenYalcinId = Guid.Parse("99999999-1111-1111-1111-111111111111");
    private static readonly Guid TuncaySanliId = Guid.Parse("99999999-2222-2222-2222-222222222222");
    private static readonly Guid VolkanDemirelId = Guid.Parse("99999999-3333-3333-3333-333333333333");
    private static readonly Guid SenolGunesId = Guid.Parse("99999999-4444-4444-4444-444444444444");
    private static readonly Guid AliKocId = Guid.Parse("99999999-5555-5555-5555-555555555555");
    private static readonly Guid ArdaGulerId = Guid.Parse("99999999-6666-6666-6666-666666666666");
    private static readonly Guid CenkTosunId = Guid.Parse("99999999-7777-7777-7777-777777777777");
    private static readonly Guid JoseMourinhoId = Guid.Parse("12121212-0000-0000-0000-000000000001");
    private static readonly Guid SalihUcanId = Guid.Parse("12121212-0000-0000-0000-000000000002");
    private static readonly Guid TaliscaId = Guid.Parse("12121212-0000-0000-0000-000000000003");
    private static readonly Guid FerdiKadiogluId = Guid.Parse("12121212-0000-0000-0000-000000000004");
    private static readonly Guid MertGunokId = Guid.Parse("12121212-0000-0000-0000-000000000005");
    private static readonly Guid BurakYilmazId = Guid.Parse("12121212-0000-0000-0000-000000000006");
    private static readonly Guid OguzhanOzyakupId = Guid.Parse("12121212-0000-0000-0000-000000000007");
    private static readonly Guid GokhanGonulId = Guid.Parse("12121212-0000-0000-0000-000000000008");
    private static readonly Guid DembaBaId = Guid.Parse("12121212-0000-0000-0000-000000000009");
    private static readonly Guid NecipUysalId = Guid.Parse("12121212-0000-0000-0000-000000000010");
    private static readonly Guid RidvanYilmazId = Guid.Parse("12121212-0000-0000-0000-000000000011");
    private static readonly Guid ZlatanIbrahimovicId = Guid.Parse("13131313-0000-0000-0000-000000000001");
    private static readonly Guid OkanBurukId = Guid.Parse("13131313-0000-0000-0000-000000000002");
    private static readonly Guid DiegoGodinId = Guid.Parse("13131313-0000-0000-0000-000000000003");
    private static readonly Guid EmreBelozogluId = Guid.Parse("13131313-0000-0000-0000-000000000004");
    private static readonly Guid DirkKuytId = Guid.Parse("13131313-0000-0000-0000-000000000005");
    private static readonly Guid RobertoCarlosId = Guid.Parse("13131313-0000-0000-0000-000000000006");
    private static readonly Guid CanerErkinId = Guid.Parse("13131313-0000-0000-0000-000000000007");
    private static readonly Guid WesleySneijderId = Guid.Parse("13131313-0000-0000-0000-000000000008");
    private static readonly Guid VanPersieId = Guid.Parse("13131313-0000-0000-0000-000000000009");
    private static readonly Guid ArdaTuranId = Guid.Parse("13131313-0000-0000-0000-000000000010");
    private static readonly Guid IlhanMansizId = Guid.Parse("13131313-0000-0000-0000-000000000011");
    private static readonly Guid AlexTellesId = Guid.Parse("13131313-0000-0000-0000-000000000012");
    private static readonly Guid NaniId = Guid.Parse("13131313-0000-0000-0000-000000000013");
    private static readonly Guid RobinGosensId = Guid.Parse("13131313-0000-0000-0000-000000000014");
    private static readonly Guid MoussaSowId = Guid.Parse("13131313-0000-0000-0000-000000000015");
    private static readonly Guid SportCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000001");
    private static readonly Guid LogisticsCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000002");
    private static readonly Guid ProductOrderCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000003");
    private static readonly Guid HumanResourcesCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000004");
    private static readonly Guid ProcurementCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000005");
    private static readonly Guid SportScoutTeamId = Guid.Parse("30303030-0000-0000-0000-000000000001");
    private static readonly Guid SportTechnicalTeamId = Guid.Parse("30303030-0000-0000-0000-000000000002");
    private static readonly Guid SportFinanceTeamId = Guid.Parse("30303030-0000-0000-0000-000000000003");
    private static readonly Guid SportTransferTeamId = Guid.Parse("30303030-0000-0000-0000-000000000004");
    private static readonly Guid LogisticsPlanningTeamId = Guid.Parse("30303030-0000-0000-0000-000000000005");
    private static readonly Guid LogisticsWarehouseTeamId = Guid.Parse("30303030-0000-0000-0000-000000000006");
    private static readonly Guid LogisticsDeliveryTeamId = Guid.Parse("30303030-0000-0000-0000-000000000007");
    private static readonly Guid ProductIntakeTeamId = Guid.Parse("30303030-0000-0000-0000-000000000008");
    private static readonly Guid ProductStockTeamId = Guid.Parse("30303030-0000-0000-0000-000000000009");
    private static readonly Guid ProductFulfillmentTeamId = Guid.Parse("30303030-0000-0000-0000-000000000010");
    private static readonly Guid HrTalentTeamId = Guid.Parse("30303030-0000-0000-0000-000000000011");
    private static readonly Guid HrExperienceTeamId = Guid.Parse("30303030-0000-0000-0000-000000000012");
    private static readonly Guid HrPayrollTeamId = Guid.Parse("30303030-0000-0000-0000-000000000013");
    private static readonly Guid ProcurementRequestTeamId = Guid.Parse("30303030-0000-0000-0000-000000000014");
    private static readonly Guid ProcurementVendorTeamId = Guid.Parse("30303030-0000-0000-0000-000000000015");
    private static readonly Guid ProcurementBudgetTeamId = Guid.Parse("30303030-0000-0000-0000-000000000016");
    private static readonly Guid SportAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000001");
    private static readonly Guid SportUnassignedRoleId = Guid.Parse("20202020-0000-0000-0000-000000000010");
    private static readonly Guid SportStarterRoleId = Guid.Parse("20202020-0000-0000-0000-000000000002");
    private static readonly Guid SportApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000003");
    private static readonly Guid LogisticsAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000004");
    private static readonly Guid LogisticsUnassignedRoleId = Guid.Parse("20202020-0000-0000-0000-000000000011");
    private static readonly Guid LogisticsApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000005");
    private static readonly Guid ProductAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000006");
    private static readonly Guid ProductUnassignedRoleId = Guid.Parse("20202020-0000-0000-0000-000000000012");
    private static readonly Guid HumanResourcesAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000013");
    private static readonly Guid HumanResourcesUnassignedRoleId = Guid.Parse("20202020-0000-0000-0000-000000000014");
    private static readonly Guid HumanResourcesStarterRoleId = Guid.Parse("20202020-0000-0000-0000-000000000015");
    private static readonly Guid HumanResourcesApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000016");
    private static readonly Guid ProcurementAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000017");
    private static readonly Guid ProcurementUnassignedRoleId = Guid.Parse("20202020-0000-0000-0000-000000000018");
    private static readonly Guid ProcurementStarterRoleId = Guid.Parse("20202020-0000-0000-0000-000000000019");
    private static readonly Guid ProcurementApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000020");
    private static readonly Guid LogisticsStarterRoleId = Guid.Parse("20202020-0000-0000-0000-000000000021");
    private static readonly Guid ProductStarterRoleId = Guid.Parse("20202020-0000-0000-0000-000000000022");
    private static readonly Guid ProductApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000023");

    public static async Task SeedAsync(AppDbContext db, bool seedMockData = true, CancellationToken cancellationToken = default)
    {
        await SeedCommunitiesAsync(db, cancellationToken);
        await SeedUsersAsync(db, cancellationToken);
        await SeedTeamsAsync(db, cancellationToken);

        if (seedMockData)
        {
            await SeedMockWorkflowDataAsync(db, cancellationToken);
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var seedUsers = BuildSeedUsers();
        if (await db.Users.AnyAsync(cancellationToken))
        {
            await UpgradePlainTextPasswordsAsync(db, cancellationToken);
            await AddMissingSeedUsersAsync(db, seedUsers, cancellationToken);
            await EnsureMissingMembershipsAsync(db, cancellationToken);
            await NormalizeLegacyPlatformRolesAsync(db, cancellationToken);
            return;
        }

        db.Users.AddRange(seedUsers);

        await db.SaveChangesAsync(cancellationToken);
        await EnsureMissingMembershipsAsync(db, cancellationToken);
        await NormalizeLegacyPlatformRolesAsync(db, cancellationToken);
    }

    private static async Task SeedCommunitiesAsync(AppDbContext db, CancellationToken cancellationToken)
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

    private static async Task SeedTeamsAsync(AppDbContext db, CancellationToken cancellationToken)
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

    private static IReadOnlyList<User> BuildSeedUsers() =>
        [
            new User
            {
                Id = AdminId,
                Username = "admin",
                DisplayName = "Admin User",
                Email = "admin@techyouth.local",
                Password = PasswordHasher.Hash("admin123"),
                Role = Role.SuperAdmin,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                CommunityMemberships = []
            },
            new User
            {
                Id = UserId,
                Username = "user",
                DisplayName = "Process Starter",
                Email = "user@techyouth.local",
                Password = PasswordHasher.Hash("user123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-29),
                CommunityMemberships = [Membership(UserId, SportCommunityId, SportStarterRoleId)]
            },
            new User
            {
                Id = ApproverId,
                Username = "approver",
                DisplayName = "Task Reviewer",
                Email = "approver@techyouth.local",
                Password = PasswordHasher.Hash("approver123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-28),
                CommunityMemberships = [Membership(ApproverId, SportCommunityId, SportApproverRoleId)]
            },
            new User
            {
                Id = MarioGomezId,
                Username = "mario.gomez",
                DisplayName = "Mario Gomez",
                Email = "mario.gomez@techyouth.local",
                Password = PasswordHasher.Hash("mario123"),
                Role = Role.User,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                CommunityMemberships = [Membership(MarioGomezId, SportCommunityId, SportUnassignedRoleId)]
            },
            new User
            {
                Id = QuaresmaId,
                Username = "quaresma",
                DisplayName = "Ricardo Quaresma",
                Email = "quaresma@techyouth.local",
                Password = PasswordHasher.Hash("trivela123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                CommunityMemberships = [Membership(QuaresmaId, SportCommunityId, SportApproverRoleId)]
            },
            new User
            {
                Id = AtibaId,
                Username = "atiba",
                DisplayName = "Atiba Hutchinson",
                Email = "atiba@techyouth.local",
                Password = PasswordHasher.Hash("atiba123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-11),
                CommunityMemberships = [Membership(AtibaId, LogisticsCommunityId, LogisticsApproverRoleId)]
            },
            new User
            {
                Id = AlexId,
                Username = "alex",
                DisplayName = "Alex de Souza",
                Email = "alex@techyouth.local",
                Password = PasswordHasher.Hash("alex123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                CommunityMemberships = [Membership(AlexId, ProductOrderCommunityId, ProductAdminRoleId)]
            },
            new User
            {
                Id = FatihTerimId,
                Username = "fatih.terim",
                DisplayName = "Fatih Terim",
                Email = "fatih.terim@techyouth.local",
                Password = PasswordHasher.Hash("imparator123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                CommunityMemberships = [Membership(FatihTerimId, SportCommunityId, SportAdminRoleId)]
            },
            new User
            {
                Id = SergenYalcinId,
                Username = "sergen.yalcin",
                DisplayName = "Sergen Yalcin",
                Email = "sergen.yalcin@techyouth.local",
                Password = PasswordHasher.Hash("sergen123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                CommunityMemberships = [Membership(SergenYalcinId, LogisticsCommunityId, LogisticsApproverRoleId)]
            },
            new User
            {
                Id = TuncaySanliId,
                Username = "tuncay.sanli",
                DisplayName = "Tuncay Sanli",
                Email = "tuncay.sanli@techyouth.local",
                Password = PasswordHasher.Hash("tuncay123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                CommunityMemberships = [Membership(TuncaySanliId, ProductOrderCommunityId, ProductAdminRoleId)]
            },
            new User
            {
                Id = VolkanDemirelId,
                Username = "volkan.demirel",
                DisplayName = "Volkan Demirel",
                Email = "volkan.demirel@techyouth.local",
                Password = PasswordHasher.Hash("volkan123"),
                Role = Role.User,
                Status = UserStatus.Rejected,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                CommunityMemberships = [Membership(VolkanDemirelId, LogisticsCommunityId, LogisticsApproverRoleId)]
            },
            new User
            {
                Id = SenolGunesId,
                Username = "senol.gunes",
                DisplayName = "Senol Gunes",
                Email = "senol.gunes@techyouth.local",
                Password = PasswordHasher.Hash("senol123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                CommunityMemberships = [Membership(SenolGunesId, HumanResourcesCommunityId, HumanResourcesAdminRoleId)]
            },
            new User
            {
                Id = AliKocId,
                Username = "ali.koc",
                DisplayName = "Ali Koc",
                Email = "ali.koc@techyouth.local",
                Password = PasswordHasher.Hash("ali123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-9),
                CommunityMemberships = [Membership(AliKocId, ProcurementCommunityId, ProcurementAdminRoleId)]
            },
            new User
            {
                Id = ArdaGulerId,
                Username = "arda.guler",
                DisplayName = "Arda Guler",
                Email = "arda.guler@techyouth.local",
                Password = PasswordHasher.Hash("arda123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                CommunityMemberships = [Membership(ArdaGulerId, HumanResourcesCommunityId, HumanResourcesStarterRoleId)]
            },
            new User
            {
                Id = CenkTosunId,
                Username = "cenk.tosun",
                DisplayName = "Cenk Tosun",
                Email = "cenk.tosun@techyouth.local",
                Password = PasswordHasher.Hash("cenk123"),
                Role = Role.User,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                CommunityMemberships = [Membership(CenkTosunId, ProcurementCommunityId, ProcurementUnassignedRoleId)]
            },
            new User
            {
                Id = JoseMourinhoId,
                Username = "jose.mourinho",
                DisplayName = "Jose Mourinho",
                Email = "jose.mourinho@techyouth.local",
                Password = PasswordHasher.Hash("jose123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                CommunityMemberships = [Membership(JoseMourinhoId, LogisticsCommunityId, LogisticsStarterRoleId)]
            },
            new User
            {
                Id = SalihUcanId,
                Username = "salih.ucan",
                DisplayName = "Salih Ucan",
                Email = "salih.ucan@techyouth.local",
                Password = PasswordHasher.Hash("salih123"),
                Role = Role.User,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CommunityMemberships = [Membership(SalihUcanId, LogisticsCommunityId, LogisticsUnassignedRoleId)]
            },
            new User
            {
                Id = TaliscaId,
                Username = "talisca",
                DisplayName = "Anderson Talisca",
                Email = "talisca@techyouth.local",
                Password = PasswordHasher.Hash("talisca123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                CommunityMemberships = [Membership(TaliscaId, ProductOrderCommunityId, ProductStarterRoleId)]
            },
            new User
            {
                Id = FerdiKadiogluId,
                Username = "ferdi.kadioglu",
                DisplayName = "Ferdi Kadioglu",
                Email = "ferdi.kadioglu@techyouth.local",
                Password = PasswordHasher.Hash("ferdi123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                CommunityMemberships = [Membership(FerdiKadiogluId, ProductOrderCommunityId, ProductApproverRoleId)]
            },
            new User
            {
                Id = MertGunokId,
                Username = "mert.gunok",
                DisplayName = "Mert Gunok",
                Email = "mert.gunok@techyouth.local",
                Password = PasswordHasher.Hash("mert123"),
                Role = Role.User,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                CommunityMemberships = [Membership(MertGunokId, ProductOrderCommunityId, ProductUnassignedRoleId)]
            },
            new User
            {
                Id = BurakYilmazId,
                Username = "burak.yilmaz",
                DisplayName = "Burak Yilmaz",
                Email = "burak.yilmaz@techyouth.local",
                Password = PasswordHasher.Hash("burak123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                CommunityMemberships = [Membership(BurakYilmazId, HumanResourcesCommunityId, HumanResourcesApproverRoleId)]
            },
            new User
            {
                Id = OguzhanOzyakupId,
                Username = "oguzhan.ozyakup",
                DisplayName = "Oguzhan Ozyakup",
                Email = "oguzhan.ozyakup@techyouth.local",
                Password = PasswordHasher.Hash("oguzhan123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                CommunityMemberships = [Membership(OguzhanOzyakupId, HumanResourcesCommunityId, HumanResourcesStarterRoleId)]
            },
            new User
            {
                Id = GokhanGonulId,
                Username = "gokhan.gonul",
                DisplayName = "Gokhan Gonul",
                Email = "gokhan.gonul@techyouth.local",
                Password = PasswordHasher.Hash("gokhan123"),
                Role = Role.User,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CommunityMemberships = [Membership(GokhanGonulId, HumanResourcesCommunityId, HumanResourcesUnassignedRoleId)]
            },
            new User
            {
                Id = DembaBaId,
                Username = "demba.ba",
                DisplayName = "Demba Ba",
                Email = "demba.ba@techyouth.local",
                Password = PasswordHasher.Hash("demba123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                CommunityMemberships = [Membership(DembaBaId, ProcurementCommunityId, ProcurementStarterRoleId)]
            },
            new User
            {
                Id = NecipUysalId,
                Username = "necip.uysal",
                DisplayName = "Necip Uysal",
                Email = "necip.uysal@techyouth.local",
                Password = PasswordHasher.Hash("necip123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                CommunityMemberships = [Membership(NecipUysalId, ProcurementCommunityId, ProcurementApproverRoleId)]
            },
            new User
            {
                Id = RidvanYilmazId,
                Username = "ridvan.yilmaz",
                DisplayName = "Ridvan Yilmaz",
                Email = "ridvan.yilmaz@techyouth.local",
                Password = PasswordHasher.Hash("ridvan123"),
                Role = Role.User,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CommunityMemberships = [Membership(RidvanYilmazId, ProcurementCommunityId, ProcurementUnassignedRoleId)]
            },
            SeedUser(ZlatanIbrahimovicId, "zlatan.ibrahimovic", "Zlatan Ibrahimovic", "zlatan123"),
            SeedUser(OkanBurukId, "okan.buruk", "Okan Buruk", "okan123"),
            SeedUser(DiegoGodinId, "diego.godin", "Diego Godin", "godin123"),
            SeedUser(EmreBelozogluId, "emre.belozoglu", "Emre Belozoglu", "emre123"),
            SeedUser(DirkKuytId, "dirk.kuyt", "Dirk Kuyt", "kuyt123"),
            SeedUser(RobertoCarlosId, "roberto.carlos", "Roberto Carlos", "carlos123"),
            SeedUser(CanerErkinId, "caner.erkin", "Caner Erkin", "caner123"),
            SeedUser(WesleySneijderId, "wesley.sneijder", "Wesley Sneijder", "wesley123"),
            SeedUser(VanPersieId, "van.persie", "Robin van Persie", "robin123"),
            SeedUser(ArdaTuranId, "arda.turan", "Arda Turan", "arda123"),
            SeedUser(IlhanMansizId, "ilhan.mansiz", "Ilhan Mansiz", "ilhan123"),
            SeedUser(AlexTellesId, "alex.telles", "Alex Telles", "telles123"),
            SeedUser(NaniId, "nani", "Luis Nani", "nani123"),
            SeedUser(RobinGosensId, "robin.gosens", "Robin Gosens", "gosens123"),
            SeedUser(MoussaSowId, "moussa.sow", "Moussa Sow", "sow123")
        ];

    private static User SeedUser(Guid id, string username, string displayName, string password) => new()
    {
        Id = id,
        Username = username,
        DisplayName = displayName,
        Email = $"{username}@techyouth.local",
        Password = PasswordHasher.Hash(password),
        Role = Role.User,
        Status = UserStatus.Active,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-4),
        CommunityMemberships = []
    };

    private static UserCommunityMembership Membership(Guid userId, Guid communityId, Guid communityRoleId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CommunityId = communityId,
            CommunityRoleId = communityRoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-25)
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

    private static async Task EnsureVersionedWorkflowSeedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var forms = await db.FormDefinitions
            .Include(form => form.Fields)
            .ThenInclude(field => field.ValidationRules)
            .Include(form => form.Versions)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var form in forms.Where(form => form.Versions.Count == 0))
        {
            db.FormDefinitionVersions.Add(FormVersionModel.BuildLegacyPublishedVersion(
                form,
                1,
                form.CreatedByUserId,
                now));
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var transferFormVersion = await db.FormDefinitionVersions
            .Where(version => version.FormDefinitionId == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")
                && version.Status == DefinitionVersionStatus.Published)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (transferFormVersion is null)
        {
            return;
        }

        var definitionId = Guid.Parse("abababab-0000-0000-0000-000000000001");
        var versionId = Guid.Parse("abababab-1000-0000-0000-000000000001");
        var definition = await db.ProcessDefinitions
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            definition = new ProcessDefinition
            {
                Id = definitionId,
                Name = "Legacy Basic Approval",
                Description = "Versioned compatibility workflow for the original one-step approval path.",
                CommunityId = SportCommunityId,
                CreatedByUserId = AdminId,
                CreatedAt = now
            };
            db.ProcessDefinitions.Add(definition);
        }

        if (definition.Versions.All(version => version.Id != versionId))
        {
            var graph = new ProcessGraphDto(
                "1.0",
                [
                    new ProcessNodeDto(
                        "start",
                        ProcessNodeType.Start,
                        "Start",
                        transferFormVersion.Id,
                        PositionX: 80,
                        PositionY: 120,
                        Width: 160,
                        Height: 72,
                        Description: "Submitted through the legacy-compatible start form."),
                    new ProcessNodeDto(
                        "approval",
                        ProcessNodeType.UserTask,
                        "Approval",
                        Priority: TaskPriority.Normal,
                        Actions: [WorkflowAction.Approve, WorkflowAction.Reject],
                        Assignment: new TaskAssignmentDto(
                            TaskAssignmentType.CommunityRole,
                            CommunityRoleId: SportApproverRoleId),
                        PositionX: 340,
                        PositionY: 110,
                        Width: 220,
                        Height: 96,
                        Description: "Community approver decision."),
                    new ProcessNodeDto(
                        "completed",
                        ProcessNodeType.CompletedEnd,
                        "Completed",
                        PositionX: 680,
                        PositionY: 60,
                        Width: 160,
                        Height: 72),
                    new ProcessNodeDto(
                        "rejected",
                        ProcessNodeType.RejectedEnd,
                        "Rejected",
                        PositionX: 680,
                        PositionY: 190,
                        Width: 160,
                        Height: 72)
                ],
                [
                    new ProcessEdgeDto("start", "approval", Order: 0, Label: "Submit"),
                    new ProcessEdgeDto("approval", "completed", WorkflowAction.Approve, Order: 1, Label: "Approve"),
                    new ProcessEdgeDto("approval", "rejected", WorkflowAction.Reject, Order: 2, Label: "Reject")
                ]);
            definition.Versions.Add(new ProcessDefinitionVersion
            {
                Id = versionId,
                ProcessDefinitionId = definitionId,
                VersionNumber = 1,
                Status = DefinitionVersionStatus.Published,
                FormDefinitionVersionId = transferFormVersion.Id,
                GraphJson = Serialize(graph),
                CreatedByUserId = AdminId,
                CreatedAt = now,
                PublishedByUserId = AdminId,
                PublishedAt = now
            });
        }

        var taskFormIds = new[]
        {
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"),
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005")
        };
        var taskFormVersions = (await db.FormDefinitionVersions
                .Where(version => taskFormIds.Contains(version.FormDefinitionId)
                    && version.Status == DefinitionVersionStatus.Published)
                .ToListAsync(cancellationToken))
            .GroupBy(version => version.FormDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(version => version.VersionNumber).First());

        if (taskFormIds.All(taskFormVersions.ContainsKey))
        {
            var transferWorkflowId = Guid.Parse("abababab-0000-0000-0000-000000000002");
            var transferWorkflowVersionId = Guid.Parse("abababab-1000-0000-0000-000000000002");
            var transferWorkflow = await db.ProcessDefinitions
                .Include(item => item.Versions)
                .SingleOrDefaultAsync(item => item.Id == transferWorkflowId, cancellationToken);
            if (transferWorkflow is null)
            {
                transferWorkflow = new ProcessDefinition
                {
                    Id = transferWorkflowId,
                    Name = "Transfer Talep Akisi",
                    Description = "Scout, teknik, mali ve transfer operasyon takimlarini kosullu olarak birlestiren demo BPM akisi.",
                    CommunityId = SportCommunityId,
                    CreatedByUserId = FatihTerimId,
                    CreatedAt = now
                };
                db.ProcessDefinitions.Add(transferWorkflow);
            }

            if (transferWorkflow.Versions.All(version => version.Id != transferWorkflowVersionId))
            {
                using var threshold = JsonDocument.Parse("5000000");
                var scoutFormVersion = taskFormVersions[taskFormIds[0]].Id;
                var technicalFormVersion = taskFormVersions[taskFormIds[1]].Id;
                var financeFormVersion = taskFormVersions[taskFormIds[2]].Id;
                var operationFormVersion = taskFormVersions[taskFormIds[3]].Id;
                var graph = new ProcessGraphDto(
                    "1.0",
                    [
                        new ProcessNodeDto(
                            "scout-lane",
                            ProcessNodeType.TeamSwimlane,
                            "Scout Ekibi",
                            PositionX: 40,
                            PositionY: 20,
                            Width: 1120,
                            Height: 220,
                            Description: "Oyuncu izleme ve ilk rapor.",
                            TeamId: SportScoutTeamId),
                        new ProcessNodeDto(
                            "technical-lane",
                            ProcessNodeType.TeamSwimlane,
                            "Teknik Degerlendirme",
                            PositionX: 40,
                            PositionY: 280,
                            Width: 1120,
                            Height: 220,
                            Description: "Kadro ve teknik uygunluk karari.",
                            TeamId: SportTechnicalTeamId),
                        new ProcessNodeDto(
                            "finance-lane",
                            ProcessNodeType.TeamSwimlane,
                            "Mali Isler",
                            PositionX: 40,
                            PositionY: 540,
                            Width: 1120,
                            Height: 220,
                            Description: "Yuksek butceli taleplerin mali kontrolu.",
                            TeamId: SportFinanceTeamId),
                        new ProcessNodeDto(
                            "operation-lane",
                            ProcessNodeType.TeamSwimlane,
                            "Transfer Operasyon",
                            PositionX: 40,
                            PositionY: 800,
                            Width: 1120,
                            Height: 220,
                            Description: "Sozlesme ve tamamlama operasyonu.",
                            TeamId: SportTransferTeamId),
                        new ProcessNodeDto(
                            "start",
                            ProcessNodeType.Start,
                            "Transfer Talebi",
                            transferFormVersion.Id,
                            ParentKey: "scout-lane",
                            PositionX: 50,
                            PositionY: 70,
                            Width: 170,
                            Height: 72),
                        new ProcessNodeDto(
                            "scoutReview",
                            ProcessNodeType.UserTask,
                            "Scout Raporu",
                            scoutFormVersion,
                            TaskPriority.High,
                            [WorkflowAction.Approve, WorkflowAction.Reject],
                            new TaskAssignmentDto(
                                TaskAssignmentType.TeamAndCommunityRole,
                                TeamId: SportScoutTeamId,
                                CommunityRoleId: SportApproverRoleId),
                            "scout-lane",
                            330,
                            58,
                            230,
                            104,
                            "Scout raporunu doldur ve ilk karari ver."),
                        new ProcessNodeDto(
                            "technicalReview",
                            ProcessNodeType.UserTask,
                            "Teknik Degerlendirme",
                            technicalFormVersion,
                            TaskPriority.High,
                            [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack],
                            new TaskAssignmentDto(
                                TaskAssignmentType.TeamAndCommunityRole,
                                TeamId: SportTechnicalTeamId,
                                CommunityRoleId: SportApproverRoleId),
                            "technical-lane",
                            300,
                            58,
                            240,
                            104,
                            "Teknik ekip kadro uygunlugunu degerlendirir."),
                        new ProcessNodeDto(
                            "budgetGateway",
                            ProcessNodeType.ExclusiveGateway,
                            "Butce Kontrolu",
                            ParentKey: "technical-lane",
                            PositionX: 650,
                            PositionY: 60,
                            Width: 180,
                            Height: 96,
                            Description: "Bes milyon uzeri talepler Mali Isler'e gider."),
                        new ProcessNodeDto(
                            "financeApproval",
                            ProcessNodeType.UserTask,
                            "Mali Onay",
                            financeFormVersion,
                            TaskPriority.Critical,
                            [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack],
                            new TaskAssignmentDto(
                                TaskAssignmentType.TeamAndCommunityRole,
                                TeamId: SportFinanceTeamId,
                                CommunityRoleId: SportApproverRoleId),
                            "finance-lane",
                            330,
                            58,
                            230,
                            104,
                            "Butce uygunlugu ve onaylanan tutar kaydedilir."),
                        new ProcessNodeDto(
                            "transferOperation",
                            ProcessNodeType.UserTask,
                            "Transfer Operasyon",
                            operationFormVersion,
                            TaskPriority.Normal,
                            [WorkflowAction.Complete, WorkflowAction.SendBack],
                            new TaskAssignmentDto(
                                TaskAssignmentType.Team,
                                TeamId: SportTransferTeamId),
                            "operation-lane",
                            330,
                            58,
                            240,
                            104,
                            "Sozlesme ve transfer tamamlama bilgileri girilir."),
                        new ProcessNodeDto(
                            "completed",
                            ProcessNodeType.CompletedEnd,
                            "Transfer Tamamlandi",
                            ParentKey: "operation-lane",
                            PositionX: 740,
                            PositionY: 70,
                            Width: 190,
                            Height: 72),
                        new ProcessNodeDto(
                            "rejected",
                            ProcessNodeType.RejectedEnd,
                            "Talep Reddedildi",
                            ParentKey: "technical-lane",
                            PositionX: 900,
                            PositionY: 70,
                            Width: 190,
                            Height: 72)
                    ],
                    [
                        new ProcessEdgeDto("start", "scoutReview", Order: 0, Label: "Talebi gonder"),
                        new ProcessEdgeDto("scoutReview", "technicalReview", WorkflowAction.Approve, Order: 1, Label: "Scout olumlu"),
                        new ProcessEdgeDto("scoutReview", "rejected", WorkflowAction.Reject, Order: 2, Label: "Scout reddi"),
                        new ProcessEdgeDto("technicalReview", "budgetGateway", WorkflowAction.Approve, Order: 3, Label: "Teknik onay"),
                        new ProcessEdgeDto("technicalReview", "rejected", WorkflowAction.Reject, Order: 4, Label: "Teknik ret"),
                        new ProcessEdgeDto("technicalReview", "scoutReview", WorkflowAction.SendBack, Order: 5, Label: "Scout'a geri gonder"),
                        new ProcessEdgeDto(
                            "budgetGateway",
                            "financeApproval",
                            Condition: new ProcessConditionDto(
                                "start.bonservis",
                                GraphConditionOperator.GreaterThan,
                                threshold.RootElement.Clone()),
                            Order: 6,
                            Label: "5M uzeri"),
                        new ProcessEdgeDto("budgetGateway", "transferOperation", IsDefault: true, Order: 7, Label: "Standart butce"),
                        new ProcessEdgeDto("financeApproval", "transferOperation", WorkflowAction.Approve, Order: 8, Label: "Mali onay"),
                        new ProcessEdgeDto("financeApproval", "rejected", WorkflowAction.Reject, Order: 9, Label: "Mali ret"),
                        new ProcessEdgeDto("financeApproval", "technicalReview", WorkflowAction.SendBack, Order: 10, Label: "Teknik ekibe don"),
                        new ProcessEdgeDto("transferOperation", "completed", WorkflowAction.Complete, Order: 11, Label: "Transferi tamamla"),
                        new ProcessEdgeDto("transferOperation", "technicalReview", WorkflowAction.SendBack, Order: 12, Label: "Teknik ekibe don")
                    ]);
                transferWorkflow.Versions.Add(new ProcessDefinitionVersion
                {
                    Id = transferWorkflowVersionId,
                    ProcessDefinitionId = transferWorkflowId,
                    VersionNumber = 1,
                    Status = DefinitionVersionStatus.Published,
                    FormDefinitionVersionId = transferFormVersion.Id,
                    GraphJson = Serialize(graph),
                    CreatedByUserId = FatihTerimId,
                    CreatedAt = now,
                    PublishedByUserId = FatihTerimId,
                    PublishedAt = now
                });
            }
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

    private static async Task AddMissingSeedUsersAsync(
        AppDbContext db,
        IReadOnlyList<User> seedUsers,
        CancellationToken cancellationToken)
    {
        var existingUsernames = await db.Users
            .Select(user => user.Username)
            .ToListAsync(cancellationToken);
        var existingEmails = await db.Users
            .Select(user => user.Email)
            .ToListAsync(cancellationToken);
        var usernameSet = existingUsernames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emailSet = existingEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingUsers = seedUsers
            .Where(user => !usernameSet.Contains(user.Username) && !emailSet.Contains(user.Email))
            .ToArray();

        if (missingUsers.Length > 0)
        {
            // Existing databases can contain system roles with different IDs. Add users first,
            // then resolve their membership through the current community template keys below.
            foreach (var user in missingUsers)
            {
                user.CommunityMemberships.Clear();
            }

            db.Users.AddRange(missingUsers);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task NormalizeLegacyPlatformRolesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(user => user.Role != Role.SuperAdmin && user.Role != Role.User)
            .ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            user.Role = Role.User;
        }

        if (users.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureMissingMembershipsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var desiredMemberships = new Dictionary<Guid, (Guid CommunityId, string TemplateKey)>
        {
            [UserId] = (SportCommunityId, CommunityRoleTemplates.ProcessStarter),
            [ApproverId] = (SportCommunityId, CommunityRoleTemplates.Approver),
            [MarioGomezId] = (SportCommunityId, CommunityRoleTemplates.Unassigned),
            [QuaresmaId] = (SportCommunityId, CommunityRoleTemplates.Approver),
            [AtibaId] = (LogisticsCommunityId, CommunityRoleTemplates.Approver),
            [AlexId] = (ProductOrderCommunityId, CommunityRoleTemplates.CommunityAdmin),
            [FatihTerimId] = (SportCommunityId, CommunityRoleTemplates.CommunityAdmin),
            [SergenYalcinId] = (LogisticsCommunityId, CommunityRoleTemplates.Approver),
            [TuncaySanliId] = (ProductOrderCommunityId, CommunityRoleTemplates.CommunityAdmin),
            [VolkanDemirelId] = (LogisticsCommunityId, CommunityRoleTemplates.Approver),
            [SenolGunesId] = (HumanResourcesCommunityId, CommunityRoleTemplates.CommunityAdmin),
            [AliKocId] = (ProcurementCommunityId, CommunityRoleTemplates.CommunityAdmin),
            [ArdaGulerId] = (HumanResourcesCommunityId, CommunityRoleTemplates.ProcessStarter),
            [CenkTosunId] = (ProcurementCommunityId, CommunityRoleTemplates.Unassigned),
            [JoseMourinhoId] = (LogisticsCommunityId, CommunityRoleTemplates.ProcessStarter),
            [SalihUcanId] = (LogisticsCommunityId, CommunityRoleTemplates.Unassigned),
            [TaliscaId] = (ProductOrderCommunityId, CommunityRoleTemplates.ProcessStarter),
            [FerdiKadiogluId] = (ProductOrderCommunityId, CommunityRoleTemplates.Approver),
            [MertGunokId] = (ProductOrderCommunityId, CommunityRoleTemplates.Unassigned),
            [BurakYilmazId] = (HumanResourcesCommunityId, CommunityRoleTemplates.Approver),
            [OguzhanOzyakupId] = (HumanResourcesCommunityId, CommunityRoleTemplates.ProcessStarter),
            [GokhanGonulId] = (HumanResourcesCommunityId, CommunityRoleTemplates.Unassigned),
            [DembaBaId] = (ProcurementCommunityId, CommunityRoleTemplates.ProcessStarter),
            [NecipUysalId] = (ProcurementCommunityId, CommunityRoleTemplates.Approver),
            [RidvanYilmazId] = (ProcurementCommunityId, CommunityRoleTemplates.Unassigned),
            [ZlatanIbrahimovicId] = (SportCommunityId, CommunityRoleTemplates.FormDesigner),
            [OkanBurukId] = (SportCommunityId, CommunityRoleTemplates.StandardUser),
            [DiegoGodinId] = (SportCommunityId, CommunityRoleTemplates.ReadOnly),
            [EmreBelozogluId] = (LogisticsCommunityId, CommunityRoleTemplates.FormDesigner),
            [DirkKuytId] = (LogisticsCommunityId, CommunityRoleTemplates.StandardUser),
            [RobertoCarlosId] = (LogisticsCommunityId, CommunityRoleTemplates.ReadOnly),
            [CanerErkinId] = (ProductOrderCommunityId, CommunityRoleTemplates.FormDesigner),
            [WesleySneijderId] = (ProductOrderCommunityId, CommunityRoleTemplates.StandardUser),
            [VanPersieId] = (ProductOrderCommunityId, CommunityRoleTemplates.ReadOnly),
            [ArdaTuranId] = (HumanResourcesCommunityId, CommunityRoleTemplates.FormDesigner),
            [IlhanMansizId] = (HumanResourcesCommunityId, CommunityRoleTemplates.StandardUser),
            [AlexTellesId] = (HumanResourcesCommunityId, CommunityRoleTemplates.ReadOnly),
            [NaniId] = (ProcurementCommunityId, CommunityRoleTemplates.FormDesigner),
            [RobinGosensId] = (ProcurementCommunityId, CommunityRoleTemplates.StandardUser),
            [MoussaSowId] = (ProcurementCommunityId, CommunityRoleTemplates.ReadOnly)
        };

        var desiredCommunityIds = desiredMemberships.Values
            .Select(desired => desired.CommunityId)
            .Distinct()
            .ToArray();
        var roleIdsByTemplate = await db.CommunityRoles
            .Where(role => desiredCommunityIds.Contains(role.CommunityId))
            .Select(role => new { role.Id, role.CommunityId, role.TemplateKey })
            .ToListAsync(cancellationToken);
        var admin = await db.Users.SingleOrDefaultAsync(user => user.Id == AdminId, cancellationToken);
        if (admin is not null)
        {
            admin.Role = Role.SuperAdmin;
            admin.Status = UserStatus.Active;
        }

        var users = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == AdminId || desiredMemberships.Keys.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        var activeMembershipsByUser = (await db.UserCommunityMemberships
            .Where(membership => desiredMemberships.Keys.Contains(membership.UserId) && membership.IsActive)
                .ToListAsync(cancellationToken))
            .GroupBy(membership => membership.UserId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAt).First());

        foreach (var userId in users)
        {
            if (userId == AdminId)
            {
                continue;
            }

            var desired = desiredMemberships[userId];
            var communityRole = roleIdsByTemplate.SingleOrDefault(role =>
                role.CommunityId == desired.CommunityId
                && role.TemplateKey.Equals(desired.TemplateKey, StringComparison.OrdinalIgnoreCase));
            if (communityRole is null)
            {
                throw new InvalidOperationException($"Seed role '{desired.TemplateKey}' was not found for community '{desired.CommunityId}'.");
            }

            if (activeMembershipsByUser.TryGetValue(userId, out var activeMembership))
            {
                // Keep the deterministic mock accounts aligned with their documented demo roles,
                // while leaving manually created users outside this seed map untouched.
                activeMembership.CommunityId = desired.CommunityId;
                activeMembership.CommunityRoleId = communityRole.Id;
                continue;
            }

            db.UserCommunityMemberships.Add(Membership(userId, desired.CommunityId, communityRole.Id));
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateInviteCode() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private static async Task UpgradePlainTextPasswordsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users.ToListAsync(cancellationToken);
        var changed = false;

        foreach (var user in users.Where(user => !PasswordHasher.IsHashed(user.Password)))
        {
            user.Password = PasswordHasher.Hash(user.Password);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedMockWorkflowDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var transferForm = new FormDefinition
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            Name = "Transfer Talep Formu",
            Description = "Futbolcu transferi, teknik ekip onayi ve butce degerlendirmesi icin demo form.",
            CommunityId = SportCommunityId,
            CreatedByUserId = AdminId,
            CreatedAt = DateTime.UtcNow.AddDays(-12),
            Fields =
            [
                Field("aaaaaaaa-1000-0000-0000-000000000001", "talepSahibi", "Talep Sahibi", FieldType.Text, true, 1),
                Field("aaaaaaaa-1000-0000-0000-000000000002", "oyuncuAdi", "Oyuncu Adi", FieldType.Text, true, 2),
                Field("aaaaaaaa-1000-0000-0000-000000000003", "kulup", "Kulup", FieldType.Select, true, 3, ["Besiktas", "Fenerbahce", "Galatasaray", "Serbest"]),
                Field("aaaaaaaa-1000-0000-0000-000000000004", "pozisyon", "Pozisyon", FieldType.Select, true, 4, ["Forvet", "Orta Saha", "Kanat", "Kaleci", "Teknik Direktor", "Baskan"]),
                Field("aaaaaaaa-1000-0000-0000-000000000005", "bonservis", "Tahmini Butce", FieldType.Number, true, 5),
                Field("aaaaaaaa-1000-0000-0000-000000000006", "acilMi", "Acil Degerlendirme", FieldType.Checkbox, false, 6),
                Field(
                    "aaaaaaaa-1000-0000-0000-000000000007",
                    "gerekce",
                    "Gerekce",
                    FieldType.Text,
                    false,
                    7,
                    validationRules:
                    [
                        new FieldValidationRule
                        {
                            Id = Guid.Parse("aaaaaaaa-2000-0000-0000-000000000001"),
                            RuleType = ValidationRuleType.RequiredWhen,
                            DependsOnFieldKey = "acilMi",
                            ExpectedValue = "true",
                            Message = "Acil talepler icin gerekce yazilmalidir."
                        }
                    ])
            ]
        };

        var scoutReportForm = new FormDefinition
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            Name = "Scout Degerlendirme Formu",
            Description = "Scout ekibinin oyuncu raporunu ve ilk tavsiyesini kaydeder.",
            CommunityId = SportCommunityId,
            CreatedByUserId = FatihTerimId,
            CreatedAt = DateTime.UtcNow.AddDays(-11),
            Fields =
            [
                Field("aaaaaaaa-1100-0000-0000-000000000001", "raporOzeti", "Rapor Ozeti", FieldType.Text, true, 1),
                Field(
                    "aaaaaaaa-1100-0000-0000-000000000002",
                    "scoutTavsiyesi",
                    "Scout Tavsiyesi",
                    FieldType.Select,
                    true,
                    2,
                    ["Olumlu", "Olumsuz", "Takip Edilsin"]),
                Field("aaaaaaaa-1100-0000-0000-000000000003", "izlemePuani", "Izleme Puani", FieldType.Number, true, 3)
            ]
        };

        var technicalReviewForm = new FormDefinition
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            Name = "Teknik Degerlendirme Formu",
            Description = "Teknik ekibin kadro uygunlugu kararini kaydeder.",
            CommunityId = SportCommunityId,
            CreatedByUserId = FatihTerimId,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            Fields =
            [
                Field(
                    "aaaaaaaa-1200-0000-0000-000000000001",
                    "teknikKarar",
                    "Teknik Karar",
                    FieldType.Select,
                    true,
                    1,
                    ["Uygun", "Revize Edilmeli", "Uygun Degil"]),
                Field("aaaaaaaa-1200-0000-0000-000000000002", "teknikNot", "Teknik Not", FieldType.Text, true, 2)
            ]
        };

        var financeApprovalForm = new FormDefinition
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"),
            Name = "Mali Onay Formu",
            Description = "Yuksek butceli transferlerde mali uygunluk kararini kaydeder.",
            CommunityId = SportCommunityId,
            CreatedByUserId = FatihTerimId,
            CreatedAt = DateTime.UtcNow.AddDays(-9),
            Fields =
            [
                Field("aaaaaaaa-1300-0000-0000-000000000001", "onaylananButce", "Onaylanan Butce", FieldType.Number, true, 1),
                Field("aaaaaaaa-1300-0000-0000-000000000002", "maliNot", "Mali Not", FieldType.Text, true, 2)
            ]
        };

        var transferOperationForm = new FormDefinition
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005"),
            Name = "Transfer Operasyon Formu",
            Description = "Sozlesme ve transfer tamamlama bilgilerini kaydeder.",
            CommunityId = SportCommunityId,
            CreatedByUserId = FatihTerimId,
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            Fields =
            [
                Field("aaaaaaaa-1400-0000-0000-000000000001", "sozlesmeImzalandi", "Sozlesme Imzalandi", FieldType.Checkbox, true, 1),
                Field("aaaaaaaa-1400-0000-0000-000000000002", "tamamlanmaTarihi", "Tamamlanma Tarihi", FieldType.Date, true, 2),
                Field("aaaaaaaa-1400-0000-0000-000000000003", "operasyonNotu", "Operasyon Notu", FieldType.Text, false, 3)
            ]
        };

        var campForm = new FormDefinition
        {
            Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            Name = "Kamp Hazirlik Onay Formu",
            Description = "Lojistik kamp, ekipman ve takvim taleplerini surece almak icin ikinci demo form.",
            CommunityId = LogisticsCommunityId,
            CreatedByUserId = AdminId,
            CreatedAt = DateTime.UtcNow.AddDays(-9),
            Fields =
            [
                Field("bbbbbbbb-1000-0000-0000-000000000001", "sorumlu", "Sorumlu", FieldType.Text, true, 1),
                Field("bbbbbbbb-1000-0000-0000-000000000002", "hedefKisi", "Hedef Kisi", FieldType.Text, true, 2),
                Field("bbbbbbbb-1000-0000-0000-000000000003", "kategori", "Kategori", FieldType.Select, true, 3, ["Kamp", "Ekipman", "Saglik", "Basina Aciklama"]),
                Field("bbbbbbbb-1000-0000-0000-000000000004", "tarih", "Planlanan Tarih", FieldType.Date, true, 4),
                Field("bbbbbbbb-1000-0000-0000-000000000005", "iletisim", "Iletisim E-posta", FieldType.Email, true, 5),
                Field("bbbbbbbb-1000-0000-0000-000000000006", "not", "Not", FieldType.Text, false, 6)
            ]
        };

        var leaveForm = new FormDefinition
        {
            Id = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
            Name = "Izin ve Uzaktan Calisma Talep Formu",
            Description = "Izin, uzaktan calisma ve ekip planlama taleplerini insan kaynaklari surecine alir.",
            CommunityId = HumanResourcesCommunityId,
            CreatedByUserId = SenolGunesId,
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            Fields =
            [
                Field("eeeeeeee-1000-0000-0000-000000000001", "calisan", "Calisan", FieldType.Text, true, 1),
                Field("eeeeeeee-1000-0000-0000-000000000002", "talepTipi", "Talep Tipi", FieldType.Select, true, 2, ["Yillik Izin", "Uzaktan Calisma", "Ebeveyn Izni"]),
                Field("eeeeeeee-1000-0000-0000-000000000003", "baslangicTarihi", "Baslangic Tarihi", FieldType.Date, true, 3),
                Field("eeeeeeee-1000-0000-0000-000000000004", "gunSayisi", "Gun Sayisi", FieldType.Number, true, 4),
                Field("eeeeeeee-1000-0000-0000-000000000005", "aciklama", "Aciklama", FieldType.Text, false, 5)
            ]
        };

        var purchaseForm = new FormDefinition
        {
            Id = Guid.Parse("ffffffff-0000-0000-0000-000000000001"),
            Name = "Satin Alma Talep Formu",
            Description = "Urun veya hizmet taleplerini butce ve tedarikci onayina tasir.",
            CommunityId = ProcurementCommunityId,
            CreatedByUserId = AliKocId,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            Fields =
            [
                Field("ffffffff-1000-0000-0000-000000000001", "talepSahibi", "Talep Sahibi", FieldType.Text, true, 1),
                Field("ffffffff-1000-0000-0000-000000000002", "kategori", "Kategori", FieldType.Select, true, 2, ["Ekipman", "Yazilim", "Hizmet"]),
                Field("ffffffff-1000-0000-0000-000000000003", "tutar", "Tahmini Tutar", FieldType.Number, true, 3),
                Field("ffffffff-1000-0000-0000-000000000004", "tedarikci", "Tercih Edilen Tedarikci", FieldType.Text, true, 4),
                Field("ffffffff-1000-0000-0000-000000000005", "acil", "Acil Tedarik", FieldType.Checkbox, false, 5)
            ]
        };

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == transferForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(transferForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == scoutReportForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(scoutReportForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == technicalReviewForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(technicalReviewForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == financeApprovalForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(financeApprovalForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == transferOperationForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(transferOperationForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == campForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(campForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == leaveForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(leaveForm);
        }

        if (!await db.FormDefinitions.AnyAsync(form => form.Id == purchaseForm.Id, cancellationToken))
        {
            db.FormDefinitions.Add(purchaseForm);
        }

        await EnsureExistingWorkflowCommunityScopeAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await EnsureVersionedWorkflowSeedAsync(db, cancellationToken);
        await DemoFormSeeder.SeedAsync(db, cancellationToken);
        await DemoWorkflowSeeder.SeedAsync(db, cancellationToken);
    }

    private static async Task EnsureExistingWorkflowCommunityScopeAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var forms = await db.FormDefinitions.ToListAsync(cancellationToken);
        foreach (var form in forms)
        {
            if (form.CommunityId != Guid.Empty)
            {
                continue;
            }

            form.CommunityId = form.Id == Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001")
                ? LogisticsCommunityId
                : SportCommunityId;
        }

        var processes = await db.ProcessInstances.ToListAsync(cancellationToken);
        foreach (var process in processes)
        {
            if (process.CommunityId != Guid.Empty)
            {
                continue;
            }

            process.CommunityId = process.FormDefinitionId == Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001")
                ? LogisticsCommunityId
                : SportCommunityId;
        }
    }

    private static FormFieldDefinition Field(
        string id,
        string key,
        string label,
        FieldType type,
        bool required,
        int sortOrder,
        IReadOnlyList<string>? options = null,
        IReadOnlyList<FieldValidationRule>? validationRules = null) =>
        new()
        {
            Id = Guid.Parse(id),
            Key = key,
            Label = label,
            Type = type,
            Required = required,
            SortOrder = sortOrder,
            OptionsJson = Serialize(options ?? []),
            ValidationRules = validationRules?.ToList() ?? []
        };

    private static IReadOnlyList<ProcessInstance> BuildMockProcesses(
        Guid transferFormId,
        Guid campFormId,
        Guid leaveFormId,
        Guid purchaseFormId)
    {
        var now = DateTime.UtcNow;

        return
        [
            Process(
                "cccccccc-0000-0000-0000-000000000001",
                transferFormId,
                ProcessStatus.InProgress,
                now.AddDays(-7),
                new
                {
                    talepSahibi = "Senol Gunes",
                    oyuncuAdi = "Mario Gomez",
                    kulup = "Besiktas",
                    pozisyon = "Forvet",
                    bonservis = 9000000,
                    acilMi = true,
                    gerekce = "Gol lazim, hem de hemen."
                }),
            Process(
                "cccccccc-0000-0000-0000-000000000002",
                transferFormId,
                ProcessStatus.Completed,
                now.AddDays(-6),
                new
                {
                    talepSahibi = "Ali Koc",
                    oyuncuAdi = "Alex de Souza",
                    kulup = "Fenerbahce",
                    pozisyon = "Orta Saha",
                    bonservis = 10300000,
                    acilMi = false,
                    gerekce = ""
                },
                completedAt: now.AddDays(-5).AddHours(2),
                completedByUserId: ApproverId,
                completedNote: "Efsane kontenjanindan onaylandi."),
            Process(
                "cccccccc-0000-0000-0000-000000000003",
                transferFormId,
                ProcessStatus.InProgress,
                now.AddDays(-5),
                new
                {
                    talepSahibi = "Fatih Terim",
                    oyuncuAdi = "Ricardo Quaresma",
                    kulup = "Besiktas",
                    pozisyon = "Kanat",
                    bonservis = 7200000,
                    acilMi = true,
                    gerekce = "Trivela etkisi surece de lazim."
                }),
            Process(
                "cccccccc-0000-0000-0000-000000000004",
                transferFormId,
                ProcessStatus.Rejected,
                now.AddDays(-4),
                new
                {
                    talepSahibi = "Sergen Yalcin",
                    oyuncuAdi = "Volkan Demirel",
                    kulup = "Fenerbahce",
                    pozisyon = "Kaleci",
                    bonservis = 19070000,
                    acilMi = false,
                    gerekce = ""
                },
                completedAt: now.AddDays(-3).AddHours(4),
                completedByUserId: ApproverId,
                completedNote: "Butce ve rekabet dengesi nedeniyle reddedildi."),
            Process(
                "cccccccc-0000-0000-0000-000000000005",
                campFormId,
                ProcessStatus.Completed,
                now.AddDays(-3),
                new
                {
                    sorumlu = "Atiba Hutchinson",
                    hedefKisi = "Necip Uysal",
                    kategori = "Kamp",
                    tarih = now.AddDays(10).ToString("yyyy-MM-dd"),
                    iletisim = "atiba@example.com",
                    not = "Kamp programi sakin, duzenli ve kaptan onayli."
                },
                completedAt: now.AddDays(-2),
                completedByUserId: ApproverId,
                completedNote: "Kamp plani onaylandi."),
            Process(
                "cccccccc-0000-0000-0000-000000000006",
                campFormId,
                ProcessStatus.InProgress,
                now.AddDays(-2),
                new
                {
                    sorumlu = "Tuncay Sanli",
                    hedefKisi = "Alex de Souza",
                    kategori = "Basina Aciklama",
                    tarih = now.AddDays(4).ToString("yyyy-MM-dd"),
                    iletisim = "tuncay@example.com",
                    not = "Ortak basin metni icin onay bekleniyor."
                }),
            Process(
                "cccccccc-0000-0000-0000-000000000007",
                campFormId,
                ProcessStatus.Rejected,
                now.AddDays(-1),
                new
                {
                    sorumlu = "Ali Koc",
                    hedefKisi = "Fatih Terim",
                    kategori = "Ekipman",
                    tarih = now.AddDays(12).ToString("yyyy-MM-dd"),
                    iletisim = "ali.koc@example.com",
                    not = "Ekstra motivasyon ekipmani talebi."
                },
                completedAt: now.AddHours(-12),
                completedByUserId: ApproverId,
                completedNote: "Talep kapsam disi bulundu."),
            Process(
                "cccccccc-0000-0000-0000-000000000008",
                transferFormId,
                ProcessStatus.InProgress,
                now.AddHours(-8),
                new
                {
                    talepSahibi = "Senol Gunes",
                    oyuncuAdi = "Atiba Hutchinson",
                    kulup = "Besiktas",
                    pozisyon = "Orta Saha",
                    bonservis = 1000000,
                    acilMi = false,
                    gerekce = "Denge lazim."
                }),
            Process(
                "cccccccc-0000-0000-0000-000000000009",
                transferFormId,
                ProcessStatus.Completed,
                now.AddHours(-6),
                new
                {
                    talepSahibi = "Sergen Yalcin",
                    oyuncuAdi = "Cenk Tosun",
                    kulup = "Besiktas",
                    pozisyon = "Forvet",
                    bonservis = 3200000,
                    acilMi = false,
                    gerekce = "Rotasyon gucu artirilsin."
                },
                completedAt: now.AddHours(-4),
                completedByUserId: SergenYalcinId,
                completedNote: "Forvet rotasyonu icin onaylandi."),
            Process(
                "cccccccc-0000-0000-0000-000000000010",
                campFormId,
                ProcessStatus.InProgress,
                now.AddHours(-5),
                new
                {
                    sorumlu = "Tuncay Sanli",
                    hedefKisi = "Arda Guler",
                    kategori = "Saglik",
                    tarih = now.AddDays(2).ToString("yyyy-MM-dd"),
                    iletisim = "tuncay.sanli@example.com",
                    not = "Genc oyuncu takip ve saglik kontrol listesi."
                }),
            Process(
                "cccccccc-0000-0000-0000-000000000011",
                transferFormId,
                ProcessStatus.Rejected,
                now.AddHours(-3),
                new
                {
                    talepSahibi = "Volkan Demirel",
                    oyuncuAdi = "Demba Ba",
                    kulup = "Serbest",
                    pozisyon = "Forvet",
                    bonservis = 5000000,
                    acilMi = true,
                    gerekce = "Acil gol katkisi beklentisi."
                },
                completedAt: now.AddHours(-2),
                completedByUserId: ApproverId,
                completedNote: "Acil talep gerekcesi yeterli bulunmadi."),
            Process(
                "cccccccc-0000-0000-0000-000000000012",
                campFormId,
                ProcessStatus.InProgress,
                now.AddMinutes(-90),
                new
                {
                    sorumlu = "Ali Koc",
                    hedefKisi = "Jose Mourinho",
                    kategori = "Basina Aciklama",
                    tarih = now.AddDays(1).ToString("yyyy-MM-dd"),
                    iletisim = "ali.koc@example.com",
                    not = "Basina aciklama taslagi icin onay bekleniyor."
                }),
            Process(
                "cccccccc-0000-0000-0000-000000000013",
                leaveFormId,
                ProcessStatus.InProgress,
                now.AddHours(-14),
                new
                {
                    calisan = "Arda Guler",
                    talepTipi = "Uzaktan Calisma",
                    baslangicTarihi = now.AddDays(3).ToString("yyyy-MM-dd"),
                    gunSayisi = 2,
                    aciklama = "Saha ziyareti sonrasi planli uzaktan calisma talebi."
                },
                startedByUserId: ArdaGulerId),
            Process(
                "cccccccc-0000-0000-0000-000000000014",
                purchaseFormId,
                ProcessStatus.Completed,
                now.AddDays(-2),
                new
                {
                    talepSahibi = "Ali Koc",
                    kategori = "Yazilim",
                    tutar = 48000,
                    tedarikci = "BPM Analytics",
                    acil = false
                },
                completedAt: now.AddDays(-1).AddHours(3),
                completedByUserId: AliKocId,
                completedNote: "Butce limiti icinde oldugu icin onaylandi.",
                startedByUserId: AliKocId)
        ];
    }

    private static ProcessInstance Process(
        string id,
        Guid formDefinitionId,
        ProcessStatus status,
        DateTime startedAt,
        object formData,
        DateTime? completedAt = null,
        Guid? completedByUserId = null,
        string? completedNote = null,
        Guid? startedByUserId = null)
    {
        var processId = Guid.Parse(id);
        var taskId = Guid.Parse(id.Replace("cccccccc", "dddddddd"));
        var startLogId = Guid.Parse(id.Replace("cccccccc", "eeeeeeee"));
        var completedLogId = Guid.Parse(id.Replace("cccccccc", "ffffffff"));

        var taskStatus = status == ProcessStatus.InProgress ? ProcessTaskStatus.Open : ProcessTaskStatus.Completed;
        var communityId = CommunityIdForForm(formDefinitionId);

        var process = new ProcessInstance
        {
            Id = processId,
            FormDefinitionId = formDefinitionId,
            CommunityId = communityId,
            StartedByUserId = startedByUserId ?? UserId,
            Status = status,
            FormDataJson = Serialize(formData),
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Tasks =
            [
                new ProcessTask
                {
                    Id = taskId,
                    AssignedRole = Role.User,
                    AssignedCommunityRoleId = ApproverRoleIdForCommunity(communityId),
                    RequiredPermission = PermissionNames.TasksAct,
                    Status = taskStatus,
                    AvailableActionsJson = Serialize(new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
                    CreatedAt = startedAt,
                    CompletedAt = completedAt,
                    CompletedByUserId = completedByUserId
                }
            ],
            AuditLogs =
            [
                new AuditLog
                {
                    Id = startLogId,
                    UserId = startedByUserId ?? UserId,
                    Action = WorkflowAction.Start,
                    FromStatus = ProcessStatus.Pending,
                    ToStatus = ProcessStatus.InProgress,
                    CreatedAt = startedAt,
                    Note = "Mock veri: formdan surec baslatildi."
                }
            ]
        };

        if (status is ProcessStatus.Completed or ProcessStatus.Rejected)
        {
            process.AuditLogs.Add(new AuditLog
            {
                Id = completedLogId,
                UserId = completedByUserId ?? ApproverId,
                Action = status == ProcessStatus.Completed ? WorkflowAction.Approve : WorkflowAction.Reject,
                FromStatus = ProcessStatus.InProgress,
                ToStatus = status,
                CreatedAt = completedAt ?? startedAt.AddHours(8),
                Note = completedNote ?? "Mock veri: aksiyon tamamlandi."
            });
        }

        return process;
    }

    private static Guid CommunityIdForForm(Guid formDefinitionId) => formDefinitionId switch
    {
        var id when id == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001") => SportCommunityId,
        var id when id == Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001") => LogisticsCommunityId,
        var id when id == Guid.Parse("eeeeeeee-0000-0000-0000-000000000001") => HumanResourcesCommunityId,
        var id when id == Guid.Parse("ffffffff-0000-0000-0000-000000000001") => ProcurementCommunityId,
        _ => SportCommunityId
    };

    private static Guid ApproverRoleIdForCommunity(Guid communityId) => communityId switch
    {
        var id when id == SportCommunityId => SportApproverRoleId,
        var id when id == LogisticsCommunityId => LogisticsApproverRoleId,
        var id when id == HumanResourcesCommunityId => HumanResourcesApproverRoleId,
        var id when id == ProcurementCommunityId => ProcurementApproverRoleId,
        _ => SportApproverRoleId
    };

    private static IReadOnlyList<SystemAuditLog> BuildMockSystemAuditLogs()
    {
        var now = DateTime.UtcNow;

        return
        [
            SystemLog(
                "99999999-0000-0000-0000-000000000001",
                MarioGomezId,
                "Auth.RegisterRequested",
                "User",
                MarioGomezId.ToString(),
                "Mario Gomez registered and is waiting for community approval.",
                now.AddDays(-6)),
            SystemLog(
                "99999999-0000-0000-0000-000000000002",
                AdminId,
                "User.AccessUpdated",
                "User",
                QuaresmaId.ToString(),
                "Ricardo Quaresma was assigned the Onay Sorumlusu community role.",
                now.AddDays(-5).AddHours(2)),
            SystemLog(
                "99999999-0000-0000-0000-000000000003",
                QuaresmaId,
                "Auth.LoginSucceeded",
                "Session",
                "demo-quaresma-session",
                "Ricardo Quaresma signed in before reviewing open tasks.",
                now.AddDays(-5).AddHours(3)),
            SystemLog(
                "99999999-0000-0000-0000-000000000004",
                AdminId,
                "FormDefinition.Updated",
                "FormDefinition",
                "aaaaaaaa-0000-0000-0000-000000000001",
                "Transfer Talep Formu field order was adjusted for demo review.",
                now.AddDays(-4).AddHours(1)),
            SystemLog(
                "99999999-0000-0000-0000-000000000005",
                AtibaId,
                "Process.Started",
                "ProcessInstance",
                "cccccccc-0000-0000-0000-000000000005",
                "Atiba Hutchinson started a camp preparation process.",
                now.AddDays(-3)),
            SystemLog(
                "99999999-0000-0000-0000-000000000006",
                ApproverId,
                "Task.Approve",
                "ProcessTask",
                "dddddddd-0000-0000-0000-000000000005",
                "Process cccccccc-0000-0000-0000-000000000005 was approved by the assigned community reviewer.",
                now.AddDays(-2)),
            SystemLog(
                "99999999-0000-0000-0000-000000000007",
                AlexId,
                "Auth.LoginFailed",
                "User",
                AlexId.ToString(),
                "Rejected user Alex de Souza attempted to sign in.",
                now.AddDays(-1).AddHours(2)),
            SystemLog(
                "99999999-0000-0000-0000-000000000008",
                FatihTerimId,
                "Auth.RegisterRequested",
                "User",
                FatihTerimId.ToString(),
                "Fatih Terim requested access to the Sportif Faaliyetler community.",
                now.AddHours(-8)),
            SystemLog(
                "99999999-0000-0000-0000-000000000009",
                AdminId,
                "User.CreatedByAdmin",
                "User",
                SergenYalcinId.ToString(),
                "SuperAdmin created Sergen Yalcin with the Onay Sorumlusu community role.",
                now.AddHours(-7)),
            SystemLog(
                "99999999-0000-0000-0000-000000000010",
                SergenYalcinId,
                "Auth.LoginSucceeded",
                "Session",
                "demo-sergen-session",
                "Sergen Yalcin signed in to review transfer requests.",
                now.AddHours(-6)),
            SystemLog(
                "99999999-0000-0000-0000-000000000011",
                TuncaySanliId,
                "Process.Started",
                "ProcessInstance",
                "cccccccc-0000-0000-0000-000000000010",
                "Tuncay Sanli started a health follow-up process.",
                now.AddHours(-5)),
            SystemLog(
                "99999999-0000-0000-0000-000000000012",
                SergenYalcinId,
                "Task.Approve",
                "ProcessTask",
                "dddddddd-0000-0000-0000-000000000009",
                "Sergen Yalcin approved a transfer rotation process.",
                now.AddHours(-4)),
            SystemLog(
                "99999999-0000-0000-0000-000000000013",
                VolkanDemirelId,
                "Auth.LoginFailed",
                "User",
                VolkanDemirelId.ToString(),
                "Rejected demo user Volkan Demirel attempted to sign in.",
                now.AddHours(-3)),
            SystemLog(
                "99999999-0000-0000-0000-000000000014",
                ApproverId,
                "Task.Reject",
                "ProcessTask",
                "dddddddd-0000-0000-0000-000000000011",
                "The assigned community reviewer rejected a late urgent transfer request.",
                now.AddHours(-2)),
            SystemLog(
                "99999999-0000-0000-0000-000000000015",
                SenolGunesId,
                "Process.Started",
                "ProcessInstance",
                "cccccccc-0000-0000-0000-000000000013",
                "Senol Gunes reviewed Arda Guler's remote work request.",
                now.AddHours(-14)),
            SystemLog(
                "99999999-0000-0000-0000-000000000016",
                AliKocId,
                "Task.Approve",
                "ProcessTask",
                "dddddddd-0000-0000-0000-000000000014",
                "Ali Koc approved a software purchase request in the Satin Alma community.",
                now.AddDays(-1).AddHours(3))
        ];
    }

    private static IReadOnlyList<Notification> BuildMockNotifications()
    {
        var now = DateTime.UtcNow;
        return
        [
            new Notification
            {
                Id = Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
                UserId = ApproverId,
                Type = "Task.Assigned",
                Title = "Onay bekleyen is atandi",
                Message = "Transfer Talep Formu icin yeni bir onay aksiyonu bekliyor.",
                EntityType = "ProcessInstance",
                EntityId = "cccccccc-0000-0000-0000-000000000001",
                CreatedAt = now.AddHours(-3)
            },
            new Notification
            {
                Id = Guid.Parse("dddddddd-0000-0000-0000-000000000002"),
                UserId = UserId,
                Type = "Process.Completed",
                Title = "Sureciniz tamamlandi",
                Message = "Baslattiginiz transfer talebi onaylandi.",
                EntityType = "ProcessInstance",
                EntityId = "cccccccc-0000-0000-0000-000000000002",
                CreatedAt = now.AddDays(-1),
                ReadAt = now.AddHours(-20)
            },
            new Notification
            {
                Id = Guid.Parse("dddddddd-0000-0000-0000-000000000003"),
                UserId = FatihTerimId,
                Type = "User.AccessUpdated",
                Title = "Topluluk yetkiniz guncellendi",
                Message = "Sportif Faaliyetler toplulugunda admin yetkiniz aktif.",
                EntityType = "User",
                EntityId = FatihTerimId.ToString(),
                CreatedAt = now.AddDays(-2)
            },
            new Notification
            {
                Id = Guid.Parse("dddddddd-0000-0000-0000-000000000004"),
                UserId = SenolGunesId,
                Type = "Task.Assigned",
                Title = "Izin talebi onay bekliyor",
                Message = "Arda Guler'in uzaktan calisma talebi icin aksiyon alinmasi gerekiyor.",
                EntityType = "ProcessInstance",
                EntityId = "cccccccc-0000-0000-0000-000000000013",
                CreatedAt = now.AddHours(-14)
            },
            new Notification
            {
                Id = Guid.Parse("dddddddd-0000-0000-0000-000000000005"),
                UserId = AliKocId,
                Type = "Process.Completed",
                Title = "Satin alma sureci tamamlandi",
                Message = "Yazilim talebi butce siniri icinde onaylandi.",
                EntityType = "ProcessInstance",
                EntityId = "cccccccc-0000-0000-0000-000000000014",
                CreatedAt = now.AddDays(-1).AddHours(3),
                ReadAt = now.AddHours(-10)
            }
        ];
    }

    private static SystemAuditLog SystemLog(
        string id,
        Guid actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        DateTime createdAt) =>
        new()
        {
            Id = Guid.Parse(id),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            CreatedAt = createdAt
        };

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
