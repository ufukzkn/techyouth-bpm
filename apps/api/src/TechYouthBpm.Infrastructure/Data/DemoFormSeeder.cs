using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Infrastructure.Data;

internal static class DemoFormSeeder
{
    internal static readonly Guid SportCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000001");
    internal static readonly Guid LogisticsCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000002");
    internal static readonly Guid ProductCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000003");
    internal static readonly Guid HumanResourcesCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000004");
    internal static readonly Guid ProcurementCommunityId = Guid.Parse("10101010-0000-0000-0000-000000000005");

    internal static readonly Guid SportStartFormId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    internal static readonly Guid SportReviewFormId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    internal static readonly Guid SportApprovalFormId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005");
    internal static readonly Guid LogisticsStartFormId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    internal static readonly Guid LogisticsReviewFormId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    internal static readonly Guid LogisticsApprovalFormId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
    internal static readonly Guid ProductStartFormId = Guid.Parse("dadadada-0000-0000-0000-000000000001");
    internal static readonly Guid ProductReviewFormId = Guid.Parse("dadadada-0000-0000-0000-000000000002");
    internal static readonly Guid ProductApprovalFormId = Guid.Parse("dadadada-0000-0000-0000-000000000003");
    internal static readonly Guid HumanResourcesStartFormId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    internal static readonly Guid HumanResourcesReviewFormId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000002");
    internal static readonly Guid HumanResourcesApprovalFormId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000003");
    internal static readonly Guid ProcurementStartFormId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    internal static readonly Guid ProcurementReviewFormId = Guid.Parse("ffffffff-0000-0000-0000-000000000002");
    internal static readonly Guid ProcurementApprovalFormId = Guid.Parse("ffffffff-0000-0000-0000-000000000003");

    private static readonly Guid SportOwnerId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid LogisticsOwnerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProductOwnerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid HumanResourcesOwnerId = Guid.Parse("99999999-4444-4444-4444-444444444444");
    private static readonly Guid ProcurementOwnerId = Guid.Parse("99999999-5555-5555-5555-555555555555");

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var specs = BuildSpecs();
        var ids = specs.Select(spec => spec.Id).ToArray();
        var existingIds = (await db.FormDefinitions
                .Where(form => ids.Contains(form.Id))
                .Select(form => form.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var missing = specs
            .Where(spec => !existingIds.Contains(spec.Id))
            .Select(BuildForm)
            .ToArray();
        if (missing.Length > 0)
        {
            db.FormDefinitions.AddRange(missing);
            await db.SaveChangesAsync(cancellationToken);
        }

        var forms = await db.FormDefinitions
            .Where(form => ids.Contains(form.Id))
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
    }

    internal static async Task<Guid> PublishedVersionIdAsync(
        AppDbContext db,
        Guid formId,
        CancellationToken cancellationToken) =>
        await db.FormDefinitionVersions
            .Where(version => version.FormDefinitionId == formId
                && version.Status == DefinitionVersionStatus.Published)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.Id)
            .FirstAsync(cancellationToken);

    private static FormDefinition BuildForm(FormSpec spec) => new()
    {
        Id = spec.Id,
        Name = spec.Name,
        Description = spec.Description,
        CommunityId = spec.CommunityId,
        CreatedByUserId = spec.OwnerId,
        CreatedAt = DateTime.UtcNow.AddDays(-20),
        Fields = spec.Fields.Select((field, index) => new FormFieldDefinition
        {
            Id = StableGuid($"demo-form:{spec.Id}:field:{field.Key}"),
            Key = field.Key,
            Label = field.Label,
            Type = field.Type,
            Required = field.Required,
            SortOrder = index + 1,
            OptionsJson = JsonHelpers.Serialize(field.Options)
        }).ToList()
    };

    private static IReadOnlyList<FormSpec> BuildSpecs() =>
    [
        // Existing sport and start forms are included so their versions are guaranteed.
        new(SportStartFormId, SportCommunityId, SportOwnerId, "Transfer Talep Formu", "Transfer surecinin baslangic verileri.", []),
        new(SportReviewFormId, SportCommunityId, SportOwnerId, "Teknik Degerlendirme Formu", "Teknik ekibin kadro uygunlugu karari.", []),
        new(SportApprovalFormId, SportCommunityId, SportOwnerId, "Transfer Operasyon Formu", "Sozlesme ve transfer tamamlama bilgileri.", []),
        new(LogisticsStartFormId, LogisticsCommunityId, LogisticsOwnerId, "Kamp Hazirlik Onay Formu", "Sevkiyat ve lojistik talebinin baslangic verileri.", []),
        new(
            LogisticsReviewFormId,
            LogisticsCommunityId,
            LogisticsOwnerId,
            "Sevkiyat Planlama Formu",
            "Rota ve kapasite kontrolu.",
            [
                new("rotaNotu", "Rota Notu", FieldType.Text, true, []),
                new("kapasiteUygun", "Kapasite Uygun", FieldType.Checkbox, true, [])
            ]),
        new(
            LogisticsApprovalFormId,
            LogisticsCommunityId,
            LogisticsOwnerId,
            "Teslimat Sonuc Formu",
            "Teslimat ve sure bilgisini kaydeder.",
            [
                new("sevkKodu", "Sevk Kodu", FieldType.Text, true, []),
                new("teslimEdildi", "Teslim Edildi", FieldType.Checkbox, true, []),
                new("sureSaat", "Toplam Sure (Saat)", FieldType.Number, true, [])
            ]),
        new(
            ProductStartFormId,
            ProductCommunityId,
            ProductOwnerId,
            "Urun Siparis Talep Formu",
            "Urun ve miktar bilgisini siparis akimina alir.",
            [
                new("talepSahibi", "Talep Sahibi", FieldType.Text, true, []),
                new("urunAdi", "Urun Adi", FieldType.Text, true, []),
                new("adet", "Adet", FieldType.Number, true, []),
                new("acil", "Acil Siparis", FieldType.Checkbox, false, [])
            ]),
        new(
            ProductReviewFormId,
            ProductCommunityId,
            ProductOwnerId,
            "Stok Kontrol Formu",
            "Stok uygunlugu ve rezervasyonu kaydeder.",
            [
                new("stokVar", "Stok Mevcut", FieldType.Checkbox, true, []),
                new("ayrilanAdet", "Ayrilan Adet", FieldType.Number, true, [])
            ]),
        new(
            ProductApprovalFormId,
            ProductCommunityId,
            ProductOwnerId,
            "Siparis Hazirlama Formu",
            "Paketleme ve cikis bilgilerini kaydeder.",
            [
                new("paketKodu", "Paket Kodu", FieldType.Text, true, []),
                new("hazirlandi", "Siparis Hazirlandi", FieldType.Checkbox, true, [])
            ]),
        new(HumanResourcesStartFormId, HumanResourcesCommunityId, HumanResourcesOwnerId, "Izin ve Uzaktan Calisma Talep Formu", "Calisan talebinin baslangic verileri.", []),
        new(
            HumanResourcesReviewFormId,
            HumanResourcesCommunityId,
            HumanResourcesOwnerId,
            "Ekip Kapasite Formu",
            "Ekip planini ve yonetici gorusunu kaydeder.",
            [
                new("ekipPlani", "Ekip Plani", FieldType.Text, true, []),
                new("yoneticiUygun", "Yonetici Uygun Buldu", FieldType.Checkbox, true, [])
            ]),
        new(
            HumanResourcesApprovalFormId,
            HumanResourcesCommunityId,
            HumanResourcesOwnerId,
            "Ozluk Kayit Formu",
            "Onaylanan talebin ozluk kaydini tamamlar.",
            [
                new("ozlukNotu", "Ozluk Notu", FieldType.Text, true, []),
                new("kaydaAlindi", "Kayda Alindi", FieldType.Checkbox, true, [])
            ]),
        new(ProcurementStartFormId, ProcurementCommunityId, ProcurementOwnerId, "Satin Alma Talep Formu", "Tedarik ve butce talebinin baslangic verileri.", []),
        new(
            ProcurementReviewFormId,
            ProcurementCommunityId,
            ProcurementOwnerId,
            "Tedarikci Degerlendirme Formu",
            "Tedarikci ve teklif karsilastirmasini kaydeder.",
            [
                new("tedarikci", "Secilen Tedarikci", FieldType.Text, true, []),
                new("teklifSayisi", "Teklif Sayisi", FieldType.Number, true, []),
                new("uygun", "Tedarikci Uygun", FieldType.Checkbox, true, [])
            ]),
        new(
            ProcurementApprovalFormId,
            ProcurementCommunityId,
            ProcurementOwnerId,
            "Butce Onay Formu",
            "Butce kararini ve onaylanan tutari kaydeder.",
            [
                new("onaylananButce", "Onaylanan Butce", FieldType.Number, true, []),
                new("butceOnaylandi", "Butce Onaylandi", FieldType.Checkbox, true, [])
            ])
    ];

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record FormSpec(
        Guid Id,
        Guid CommunityId,
        Guid OwnerId,
        string Name,
        string Description,
        IReadOnlyList<FieldSpec> Fields);

    private sealed record FieldSpec(
        string Key,
        string Label,
        FieldType Type,
        bool Required,
        IReadOnlyList<string> Options);
}
