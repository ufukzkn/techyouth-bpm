using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Forms;

public class FormVersionServiceTests
{
    [Fact]
    public async Task Legacy_Create_Also_Creates_Published_OnePage_Version()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var created = await new FormService(db).CreateAsync(
            LegacyRequest("Pinned Form", "amount"),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.True(created.IsSuccess, string.Join(" | ", created.Errors));
        Assert.NotNull(created.Value!.LatestPublishedVersionId);
        var version = await db.FormDefinitionVersions
            .Include(item => item.Pages)
            .ThenInclude(page => page.Fields)
            .SingleAsync(item => item.Id == created.Value.LatestPublishedVersionId);
        Assert.Equal(DefinitionVersionStatus.Published, version.Status);
        Assert.Equal(1, version.VersionNumber);
        var page = Assert.Single(version.Pages);
        Assert.Equal("main", page.Key);
        Assert.Equal("amount", Assert.Single(page.Fields).Key);
    }

    [Fact]
    public async Task Versioned_Designer_Create_Does_Not_Publish_Implicitly()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var request = LegacyRequest("Draft Form", "amount") with { CreatePublishedVersion = false };

        var created = await new FormService(db).CreateAsync(
            request,
            TestDbFactory.CommunityAdminDto(admin));

        Assert.True(created.IsSuccess, string.Join(" | ", created.Errors));
        Assert.Null(created.Value!.LatestPublishedVersionId);
        Assert.False(await db.FormDefinitionVersions.AnyAsync(version =>
            version.FormDefinitionId == created.Value.Id));
    }

    [Fact]
    public async Task Updating_Published_Version_Creates_Draft_And_Preserves_History()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var user = TestDbFactory.CommunityAdminDto(admin);
        var created = await new FormService(db).CreateAsync(LegacyRequest("Expense", "amount"), user);
        var originalVersionId = created.Value!.LatestPublishedVersionId!.Value;
        var service = new FormVersionService(db, new SystemAuditService(db));

        var updated = await service.UpdateAsync(
            created.Value.Id,
            originalVersionId,
            new UpdateFormVersionRequest(
            [
                new CreateFormPageRequest(
                    "request",
                    "Request",
                    "Request details",
                    1,
                    [new CreateFormFieldRequest("department", "Department", FieldType.Text, true, 1, [], [])]),
                new CreateFormPageRequest(
                    "budget",
                    "Budget",
                    "Budget details",
                    2,
                    [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 1, [], [])])
            ]),
            user);

        Assert.True(updated.IsSuccess, string.Join(" | ", updated.Errors));
        Assert.Equal(DefinitionVersionStatus.Draft, updated.Value!.Status);
        Assert.Equal(2, updated.Value.VersionNumber);
        Assert.Equal(2, updated.Value.Pages.Count);
        db.ChangeTracker.Clear();
        var original = await service.GetVersionAsync(created.Value.Id, originalVersionId, user);
        Assert.Equal(DefinitionVersionStatus.Published, original!.Status);
        Assert.Equal("amount", Assert.Single(Assert.Single(original.Pages).Fields).Key);
    }

    [Fact]
    public async Task Draft_Rejects_Field_Keys_Duplicated_Across_Pages()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var user = TestDbFactory.CommunityAdminDto(admin);
        var created = await new FormService(db).CreateAsync(LegacyRequest("Duplicate", "original"), user);
        var service = new FormVersionService(db, new SystemAuditService(db));

        var result = await service.CreateDraftAsync(
            created.Value!.Id,
            new CreateFormVersionRequest(
            [
                new CreateFormPageRequest(
                    "one",
                    "One",
                    "",
                    1,
                    [new CreateFormFieldRequest("shared", "First", FieldType.Text, true, 1, [], [])]),
                new CreateFormPageRequest(
                    "two",
                    "Two",
                    "",
                    2,
                    [new CreateFormFieldRequest("SHARED", "Second", FieldType.Text, true, 1, [], [])])
            ]),
            user);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("duplicated across", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Published_Version_Can_Be_Archived_But_Draft_Cannot()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var user = TestDbFactory.CommunityAdminDto(admin);
        var created = await new FormService(db).CreateAsync(LegacyRequest("Archive", "amount"), user);
        var publishedVersionId = created.Value!.LatestPublishedVersionId!.Value;
        var service = new FormVersionService(db, new SystemAuditService(db));

        var archived = await service.ArchiveAsync(created.Value.Id, publishedVersionId, user);
        var draft = await service.CreateDraftAsync(
            created.Value.Id,
            new CreateFormVersionRequest(
            [
                new CreateFormPageRequest(
                    "main",
                    "Main",
                    "",
                    0,
                    [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 0, [], [])])
            ]),
            user);
        var rejected = await service.ArchiveAsync(created.Value.Id, draft.Value!.Id, user);

        Assert.True(archived.IsSuccess, string.Join(" | ", archived.Errors));
        Assert.Equal(DefinitionVersionStatus.Archived, archived.Value!.Status);
        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Errors, error => error.Contains("Only published", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(db.SystemAuditLogs, log =>
            log.Action == "FormDefinitionVersion.Archived"
            && log.EntityId == publishedVersionId.ToString());
    }

    private static CreateFormRequest LegacyRequest(string name, string key) =>
        new(
            name,
            "Version test",
            [new CreateFormFieldRequest(key, "Field", FieldType.Number, true, 1, [], [])]);
}
