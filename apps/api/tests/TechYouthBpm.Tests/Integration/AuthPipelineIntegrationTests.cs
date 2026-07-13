using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Tests.Integration;

public class AuthPipelineIntegrationTests
{
    [Fact]
    public async Task Login_Sets_Secure_Session_Cookies_And_Cookie_Authenticates_Me()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var (session, cookies) = await IntegrationTestHttp.LoginAsync(client, rememberMe: true);

        Assert.Contains("techyouth_access", cookies.Keys);
        Assert.Contains("techyouth_refresh", cookies.Keys);
        Assert.Contains("techyouth_csrf", cookies.Keys);
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));

        using var meRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Get, "/api/auth/me", cookies);
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var currentUser = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", currentUser.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Protected_Endpoint_Rejects_Anonymous_And_Invalid_Sessions()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();

        using var anonymous = await client.GetAsync("/api/auth/me");
        using var invalidRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Get, "/api/auth/me", "invalid-token");
        using var invalid = await client.SendAsync(invalidRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task Cookie_Mutation_Requires_Csrf_While_Bearer_Remains_Swagger_Compatible()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, cookies) = await IntegrationTestHttp.LoginAsync(client);
        var profile = new UpdateProfileRequest("Admin User", "admin@techyouth.local");

        using var missingCsrf = IntegrationTestHttp.CookieRequest(HttpMethod.Patch, "/api/auth/me/profile", cookies);
        missingCsrf.Content = JsonContent.Create(profile);
        using var missingCsrfResponse = await client.SendAsync(missingCsrf);

        using var validCsrf = IntegrationTestHttp.CookieRequest(
            HttpMethod.Patch,
            "/api/auth/me/profile",
            cookies,
            session.CsrfToken);
        validCsrf.Content = JsonContent.Create(profile);
        using var validCsrfResponse = await client.SendAsync(validCsrf);

        using var bearer = IntegrationTestHttp.BearerRequest(HttpMethod.Patch, "/api/auth/me/profile", session.Token);
        bearer.Content = JsonContent.Create(profile);
        using var bearerResponse = await client.SendAsync(bearer);

        Assert.Equal(HttpStatusCode.BadRequest, missingCsrfResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, validCsrfResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bearerResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_Rotates_Token_And_Reuse_Revokes_The_Device_Session()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (_, loginCookies) = await IntegrationTestHttp.LoginAsync(client, rememberMe: true);
        var originalRefreshToken = loginCookies["techyouth_refresh"];

        using var refreshRequest = IntegrationTestHttp.CookieRequest(
            HttpMethod.Post,
            "/api/auth/refresh",
            new Dictionary<string, string> { ["techyouth_refresh"] = originalRefreshToken });
        using var refreshResponse = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedPayload = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshedAccessToken = refreshedPayload.GetProperty("token").GetString();
        var rotatedCookies = IntegrationTestHttp.ReadCookies(refreshResponse);
        Assert.NotEqual(originalRefreshToken, rotatedCookies["techyouth_refresh"]);

        using var reuseRequest = IntegrationTestHttp.CookieRequest(
            HttpMethod.Post,
            "/api/auth/refresh",
            new Dictionary<string, string> { ["techyouth_refresh"] = originalRefreshToken });
        using var reuseResponse = await client.SendAsync(reuseRequest);
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);

        using var meRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/auth/me",
            refreshedAccessToken!);
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_Revokes_The_Current_Access_Session()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client, rememberMe: true);

        using var logoutRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/auth/logout", session.Token);
        using var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var meRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Get, "/api/auth/me", session.Token);
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Login_Rate_Limit_Returns_Too_Many_Requests()
    {
        using var factory = new ApiWebApplicationFactory(rateLimitPermitLimit: 2);
        using var client = factory.CreateApiClient();

        using var first = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing", "wrong"));
        using var second = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing", "wrong"));
        using var third = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing", "wrong"));

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task Inactive_Community_Blocks_Member_But_Allows_Community_Admin()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        await factory.ExecuteDbAsync(async db =>
        {
            var communityId = await db.UserCommunityMemberships
                .Where(membership => membership.User!.Username == "user" && membership.IsActive)
                .Select(membership => membership.CommunityId)
                .SingleAsync();
            var community = await db.Communities.SingleAsync(item => item.Id == communityId);
            community.IsActive = false;
            await db.SaveChangesAsync();
        });

        using var memberResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("user", "user123"));
        using var adminResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("fatih.terim", "imparator123"));

        Assert.Equal(HttpStatusCode.BadRequest, memberResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }
}
