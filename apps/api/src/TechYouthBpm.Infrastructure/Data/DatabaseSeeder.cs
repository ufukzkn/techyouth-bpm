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
                Password = PasswordHasher.Hash("admin123"),
                Role = Role.Admin
            },
            new User
            {
                Id = UserId,
                Username = "user",
                DisplayName = "Process Starter",
                Password = PasswordHasher.Hash("user123"),
                Role = Role.User
            },
            new User
            {
                Id = ApproverId,
                Username = "approver",
                DisplayName = "Process Approver",
                Password = PasswordHasher.Hash("approver123"),
                Role = Role.Approver
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

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
