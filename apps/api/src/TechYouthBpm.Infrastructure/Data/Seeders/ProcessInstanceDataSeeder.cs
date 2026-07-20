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

internal static class ProcessInstanceDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task SeedAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default) =>
        SeedMockWorkflowDataAsync(db, cancellationToken);

    private static async Task EnsureVersionedWorkflowSeedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var forms = await db.FormDefinitions
            .AsSplitQuery()
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
            Category = SystemAuditCategories.Resolve(action, entityType),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            CreatedAt = createdAt
        };

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
