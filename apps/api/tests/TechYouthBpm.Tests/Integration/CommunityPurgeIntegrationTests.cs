using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Integration;

public class CommunityPurgeIntegrationTests
{
    [Fact]
    public async Task Community_Admin_Cannot_Inspect_Or_Purge_A_Community()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(
            client,
            "fatih.terim",
            "imparator123");
        var communityId = await factory.ExecuteDbAsync(db => db.UserCommunityMemberships
            .Where(membership => membership.User!.Username == "fatih.terim" && membership.IsActive)
            .Select(membership => membership.CommunityId)
            .SingleAsync());

        using var impactRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            $"/api/communities/{communityId}/deletion-impact",
            session.Token);
        using var impactResponse = await client.SendAsync(impactRequest);

        Assert.Equal(HttpStatusCode.Forbidden, impactResponse.StatusCode);

        using var archiveRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/audit/archives",
            session.Token);
        using var archiveResponse = await client.SendAsync(archiveRequest);

        Assert.Equal(HttpStatusCode.Forbidden, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task Purge_Requires_Inactive_Community_Exact_Name_Password_And_Reason()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client);
        var community = await factory.ExecuteDbAsync(db => db.Communities
            .Where(item => item.IsActive)
            .Select(item => new { item.Id, item.Name })
            .FirstAsync());

        using var request = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/communities/{community.Id}/purge",
            session.Token);
        request.Content = JsonContent.Create(new PurgeCommunityRequest(
            community.Name,
            "admin123",
            "Integration test cleanup"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await factory.ExecuteDbAsync(db => db.Communities.AnyAsync(item => item.Id == community.Id)));

        await factory.ExecuteDbAsync(async db =>
        {
            var target = await db.Communities.SingleAsync(item => item.Id == community.Id);
            target.IsActive = false;
            await db.SaveChangesAsync();
        });

        var invalidRequests = new[]
        {
            new PurgeCommunityRequest("Wrong community", "admin123", "A sufficiently detailed reason."),
            new PurgeCommunityRequest(community.Name, "wrong-password", "A sufficiently detailed reason."),
            new PurgeCommunityRequest(community.Name, "admin123", "short")
        };
        foreach (var payload in invalidRequests)
        {
            using var invalidRequest = IntegrationTestHttp.BearerRequest(
                HttpMethod.Post,
                $"/api/communities/{community.Id}/purge",
                session.Token);
            invalidRequest.Content = JsonContent.Create(payload);
            using var invalidResponse = await client.SendAsync(invalidRequest);

            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
            Assert.True(await factory.ExecuteDbAsync(db =>
                db.Communities.AnyAsync(item => item.Id == community.Id)));
        }
    }

    [Fact]
    public async Task Cookie_Based_Purge_Requires_A_Valid_Csrf_Header()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (_, cookies) = await IntegrationTestHttp.LoginAsync(client);
        var target = await factory.ExecuteDbAsync(async db =>
        {
            var community = await db.Communities.FirstAsync(item => item.Name == "Lojistik");
            community.IsActive = false;
            await db.SaveChangesAsync();
            return new { community.Id, community.Name };
        });

        using var request = IntegrationTestHttp.CookieRequest(
            HttpMethod.Post,
            $"/api/communities/{target.Id}/purge",
            cookies);
        request.Content = JsonContent.Create(new PurgeCommunityRequest(
            target.Name,
            "admin123",
            "This request intentionally omits its CSRF header."));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await factory.ExecuteDbAsync(db =>
            db.Communities.AnyAsync(item => item.Id == target.Id)));
    }

    [Fact]
    public async Task Purge_Archives_Safe_Events_And_Deletes_Only_The_Target_Community()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client);
        var target = await factory.ExecuteDbAsync(async db =>
        {
            var community = await db.Communities
                .Where(item => item.Name == "Lojistik")
                .SingleAsync();
            community.IsActive = false;
            var user = await db.UserCommunityMemberships
                .Where(membership => membership.CommunityId == community.Id
                    && membership.User!.Role != Role.SuperAdmin)
                .Select(membership => membership.User!)
                .FirstAsync();
            var otherRole = await db.CommunityRoles
                .Where(role => role.CommunityId != community.Id && role.TemplateKey == "unassigned")
                .FirstAsync();
            db.UserCommunityMemberships.Add(new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CommunityId = otherRole.CommunityId,
                CommunityRoleId = otherRole.Id,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return new { community.Id, community.Name, PreservedUserId = user.Id };
        });
        var otherCommunityCount = await factory.ExecuteDbAsync(db =>
            db.Communities.CountAsync(item => item.Id != target.Id));

        using var impactRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            $"/api/communities/{target.Id}/deletion-impact",
            session.Token);
        using var impactResponse = await client.SendAsync(impactRequest);
        Assert.True(
            impactResponse.IsSuccessStatusCode,
            await impactResponse.Content.ReadAsStringAsync());
        var impact = await impactResponse.Content.ReadFromJsonAsync<CommunityDeletionImpactDto>();
        Assert.NotNull(impact);
        Assert.True(impact.UserCount > 0);
        Assert.True(impact.CommunityRoleCount > 0);
        Assert.True(impact.ProcessCount > 0);
        Assert.True(impact.TaskCount > 0);

        using var purgeRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/communities/{target.Id}/purge",
            session.Token);
        purgeRequest.Content = JsonContent.Create(new PurgeCommunityRequest(
            target.Name,
            "admin123",
            "The community is removed by an integration test."));
        using var purgeResponse = await client.SendAsync(purgeRequest);
        var body = await purgeResponse.Content.ReadAsStringAsync();
        Assert.True(purgeResponse.IsSuccessStatusCode, body);
        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        var archiveId = payload.GetProperty("archiveId").GetGuid();

        var persisted = await factory.ExecuteDbAsync(async db => new
        {
            CommunityExists = await db.Communities.AnyAsync(item => item.Id == target.Id),
            OtherCommunityCount = await db.Communities.CountAsync(),
            PreservedUserExists = await db.Users.AnyAsync(user => user.Id == target.PreservedUserId),
            TargetMembershipExists = await db.UserCommunityMemberships.AnyAsync(
                membership => membership.CommunityId == target.Id),
            ArchiveExists = await db.CommunityDeletionArchives.AnyAsync(archive => archive.Id == archiveId),
            EventCount = await db.ArchivedAuditEvents.CountAsync(
                auditEvent => auditEvent.CommunityDeletionArchiveId == archiveId),
            RawSensitiveDataStored = await db.ArchivedAuditEvents.AnyAsync(auditEvent =>
                auditEvent.Description.Contains("@")
                || auditEvent.Description.Contains("127.0.0.1")
                || auditEvent.Description.Contains("FormDataJson"))
        });

        Assert.False(persisted.CommunityExists);
        Assert.Equal(otherCommunityCount, persisted.OtherCommunityCount);
        Assert.True(persisted.PreservedUserExists);
        Assert.False(persisted.TargetMembershipExists);
        Assert.True(persisted.ArchiveExists);
        Assert.True(persisted.EventCount > 0);
        Assert.False(persisted.RawSensitiveDataStored);

        using var archiveRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            $"/api/audit/archives/{archiveId}/logs?category=processes&page=1&pageSize=5",
            session.Token);
        using var archiveResponse = await client.SendAsync(archiveRequest);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
    }

    [Fact]
    public async Task Database_Failure_Rolls_Back_Both_Archive_And_Operational_Deletion()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client);
        var target = await factory.ExecuteDbAsync(async db =>
        {
            var community = await db.Communities.FirstAsync(item => item.Name == "Urun Siparisi");
            community.IsActive = false;
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlAsync(
                $"""
                CREATE TRIGGER fail_community_purge
                BEFORE DELETE ON "Communities"
                BEGIN
                    SELECT RAISE(ABORT, 'forced purge failure');
                END;
                """);
            return new { community.Id, community.Name };
        });
        var processCount = await factory.ExecuteDbAsync(db =>
            db.ProcessInstances.CountAsync(process => process.CommunityId == target.Id));

        using var purgeRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/communities/{target.Id}/purge",
            session.Token);
        purgeRequest.Content = JsonContent.Create(new PurgeCommunityRequest(
            target.Name,
            "admin123",
            "Force a transaction rollback for this integration test."));
        using var purgeResponse = await client.SendAsync(purgeRequest);

        Assert.Equal(HttpStatusCode.InternalServerError, purgeResponse.StatusCode);
        var persisted = await factory.ExecuteDbAsync(async db => new
        {
            CommunityExists = await db.Communities.AnyAsync(item => item.Id == target.Id),
            ProcessCount = await db.ProcessInstances.CountAsync(process => process.CommunityId == target.Id),
            ArchiveExists = await db.CommunityDeletionArchives.AnyAsync(
                archive => archive.OriginalCommunityId == target.Id)
        });
        Assert.True(persisted.CommunityExists);
        Assert.Equal(processCount, persisted.ProcessCount);
        Assert.False(persisted.ArchiveExists);
    }
}
