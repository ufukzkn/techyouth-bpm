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

internal static class IdentityDataSeeder
{
    public static Task SeedAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default) =>
        SeedUsersAsync(db, cancellationToken);

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
}
