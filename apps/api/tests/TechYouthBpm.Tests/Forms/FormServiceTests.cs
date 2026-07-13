using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Forms;

public class FormServiceTests
{
    [Fact]
    public async Task UpdateAsync_Allows_Admin_To_Update_Form_Definition()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var service = new FormService(db);

        var adminDto = TestDbFactory.ToDto(admin);
        var created = await service.CreateAsync(CreateRequest("Masraf Formu", "amount", "Tutar"), adminDto);
        var update = CreateRequest("Guncel Masraf Formu", "department", "Departman");

        var result = await service.UpdateAsync(created.Value!.Id, update, adminDto);

        Assert.True(result.IsSuccess);
        Assert.Equal("Guncel Masraf Formu", result.Value!.Name);
        Assert.Single(result.Value.Fields);
        Assert.Equal("department", result.Value.Fields[0].Key);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Non_Admin_Users()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var user = TestDbFactory.SeedUser(db, Role.User);
        var service = new FormService(db);

        var adminDto = TestDbFactory.ToDto(admin);
        var userDto = TestDbFactory.ToDto(user);
        var created = await service.CreateAsync(CreateRequest("Masraf Formu", "amount", "Tutar"), adminDto);

        var result = await service.UpdateAsync(created.Value!.Id, CreateRequest("Guncel Form", "note", "Not"), userDto);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("cannot update", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateAsync_Replaces_Fields_And_Validation_Rules()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var service = new FormService(db);

        var adminDto = TestDbFactory.ToDto(admin);
        var created = await service.CreateAsync(CreateRequest("Talep Formu", "requestType", "Talep Tipi"), adminDto);
        var update = new CreateFormRequest(
            "Talep Formu",
            "Guncellenen form modeli",
            [
                new CreateFormFieldRequest("requestType", "Talep Tipi", FieldType.Select, true, 1, ["Masraf", "Satinalma"], []),
                new CreateFormFieldRequest(
                    "approvalNote",
                    "Onay Notu",
                    FieldType.Text,
                    false,
                    2,
                    [],
                    [
                        new ValidationRuleDto(
                            ValidationRuleType.RequiredWhen,
                            "requestType",
                            "Satinalma",
                            "Satinalma talebinde onay notu zorunludur.")
                    ])
            ]);

        var result = await service.UpdateAsync(created.Value!.Id, update, adminDto);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Fields.Count);
        Assert.Equal("approvalNote", result.Value.Fields[1].Key);
        Assert.Single(result.Value.Fields[1].ValidationRules);
        Assert.DoesNotContain(result.Value.Fields, field => field.Key == "requestType" && field.Type == FieldType.Text);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Radio_Without_Options()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var service = new FormService(db);
        var request = CreateOptionRequest(FieldType.Radio, []);

        var result = await service.CreateAsync(request, TestDbFactory.ToDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("needs options", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_Rejects_Case_Insensitive_Duplicate_Radio_Options_After_Trim()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var service = new FormService(db);
        var request = CreateOptionRequest(FieldType.Radio, ["Onay", " onay "]);

        var result = await service.CreateAsync(request, TestDbFactory.ToDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("duplicate options", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_Rejects_Select_With_Empty_Option()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var service = new FormService(db);
        var request = CreateOptionRequest(FieldType.Select, ["Onay", "   "]);

        var result = await service.CreateAsync(request, TestDbFactory.ToDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("empty options", StringComparison.OrdinalIgnoreCase));
    }

    private static CreateFormRequest CreateRequest(string name, string key, string label) =>
        new(
            name,
            "Test form",
            [
                new CreateFormFieldRequest(key, label, FieldType.Text, true, 1, [], [])
            ]);

    private static CreateFormRequest CreateOptionRequest(FieldType type, IReadOnlyList<string> options) =>
        new(
            "Secim Formu",
            "Test form",
            [
                new CreateFormFieldRequest("choice", "Secim", type, true, 1, options, [])
            ]);
}
