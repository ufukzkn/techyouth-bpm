using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Security;

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

    public static async Task SeedAsync(AppDbContext db, bool seedMockData = true, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await SeedUsersAsync(db, cancellationToken);

        if (seedMockData)
        {
            await SeedMockWorkflowDataAsync(db, cancellationToken);
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            await UpgradePlainTextPasswordsAsync(db, cancellationToken);
            return;
        }

        db.Users.AddRange(
            new User
            {
                Id = AdminId,
                Username = "admin",
                DisplayName = "Admin User",
                Email = "admin@techyouth.local",
                Password = PasswordHasher.Hash("admin123"),
                Role = Role.Admin,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
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
                CreatedAt = DateTime.UtcNow.AddDays(-29)
            },
            new User
            {
                Id = ApproverId,
                Username = "approver",
                DisplayName = "Process Approver",
                Email = "approver@techyouth.local",
                Password = PasswordHasher.Hash("approver123"),
                Role = Role.Approver,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-28)
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
                CreatedAt = DateTime.UtcNow.AddDays(-6)
            },
            new User
            {
                Id = QuaresmaId,
                Username = "quaresma",
                DisplayName = "Ricardo Quaresma",
                Email = "quaresma@techyouth.local",
                Password = PasswordHasher.Hash("trivela123"),
                Role = Role.Approver,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-14)
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
                CreatedAt = DateTime.UtcNow.AddDays(-11)
            },
            new User
            {
                Id = AlexId,
                Username = "alex",
                DisplayName = "Alex de Souza",
                Email = "alex@techyouth.local",
                Password = PasswordHasher.Hash("alex123"),
                Role = Role.User,
                Status = UserStatus.Rejected,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new User
            {
                Id = FatihTerimId,
                Username = "fatih.terim",
                DisplayName = "Fatih Terim",
                Email = "fatih.terim@techyouth.local",
                Password = PasswordHasher.Hash("imparator123"),
                Role = Role.Admin,
                Status = UserStatus.PendingApproval,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            });

        await db.SaveChangesAsync(cancellationToken);
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

    private static async Task SeedMockWorkflowDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.FormDefinitions.AnyAsync(form => form.Name == "Transfer Talep Formu", cancellationToken))
        {
            return;
        }

        var transferForm = new FormDefinition
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            Name = "Transfer Talep Formu",
            Description = "Futbolcu transferi, teknik ekip onayi ve butce degerlendirmesi icin demo form.",
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

        var campForm = new FormDefinition
        {
            Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            Name = "Kamp Hazirlik Onay Formu",
            Description = "Kamp, ekipman ve takvim taleplerini surece almak icin ikinci demo form.",
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

        db.FormDefinitions.AddRange(transferForm, campForm);
        db.ProcessInstances.AddRange(BuildMockProcesses(transferForm.Id, campForm.Id));
        db.SystemAuditLogs.AddRange(BuildMockSystemAuditLogs());

        await db.SaveChangesAsync(cancellationToken);
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

    private static IReadOnlyList<ProcessInstance> BuildMockProcesses(Guid transferFormId, Guid campFormId)
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
                })
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
        string? completedNote = null)
    {
        var processId = Guid.Parse(id);
        var taskId = Guid.Parse(id.Replace("cccccccc", "dddddddd"));
        var startLogId = Guid.Parse(id.Replace("cccccccc", "eeeeeeee"));
        var completedLogId = Guid.Parse(id.Replace("cccccccc", "ffffffff"));

        var taskStatus = status == ProcessStatus.InProgress ? ProcessTaskStatus.Open : ProcessTaskStatus.Completed;

        var process = new ProcessInstance
        {
            Id = processId,
            FormDefinitionId = formDefinitionId,
            StartedByUserId = UserId,
            Status = status,
            FormDataJson = Serialize(formData),
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Tasks =
            [
                new ProcessTask
                {
                    Id = taskId,
                    AssignedRole = Role.Approver,
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
                    UserId = UserId,
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
                "Mario Gomez registered and is waiting for admin approval.",
                now.AddDays(-6)),
            SystemLog(
                "99999999-0000-0000-0000-000000000002",
                AdminId,
                "User.AccessUpdated",
                "User",
                QuaresmaId.ToString(),
                "Ricardo Quaresma was approved as Approver.",
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
                "Process cccccccc-0000-0000-0000-000000000005 was approved by Process Approver.",
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
                "Fatih Terim requested Admin-level access and is waiting for approval.",
                now.AddHours(-8))
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
