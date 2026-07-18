using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Tests.Integration;

public class AuthPipelineIntegrationTests
{
    [Fact]
    public async Task Registration_Request_Appears_In_The_Target_Community_Admin_Inbox()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        var registration = new RegisterRequest(
            "pending-http-member",
            "Pending HTTP Member",
            "pending-http-member@test.local",
            "password123",
            "SPOR1");

        using var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registeredUser = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var registeredUserId = registeredUser.GetProperty("id").GetGuid();

        var (managerSession, _) = await IntegrationTestHttp.LoginAsync(
            client,
            username: "fatih.terim",
            password: "imparator123");
        using var inboxRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/notifications?category=access&page=1&pageSize=10",
            managerSession.Token);
        using var inboxResponse = await client.SendAsync(inboxRequest);
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);
        var inbox = await inboxResponse.Content.ReadFromJsonAsync<NotificationPageDto>();

        var notification = Assert.Single(inbox!.Items.Where(item => item.Type == "User.PendingApproval"
            && item.EntityId == registeredUserId.ToString()));
        Assert.Contains("Pending HTTP Member", notification.Message, StringComparison.Ordinal);
    }

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
    public async Task Browser_Cookie_Transport_Does_Not_Expose_Auth_Tokens_In_Response_Bodies()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/browser-login")
        {
            Content = JsonContent.Create(new LoginRequest("admin", "admin123", RememberMe: true))
        };

        using var loginResponse = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginPayload.TryGetProperty("user", out var user));
        Assert.Equal("admin", user.GetProperty("username").GetString());
        Assert.True(loginPayload.TryGetProperty("expiresAt", out _));
        Assert.False(loginPayload.TryGetProperty("token", out _));
        Assert.False(loginPayload.TryGetProperty("csrfToken", out _));

        var cookies = IntegrationTestHttp.ReadCookies(loginResponse);
        Assert.Contains("techyouth_access", cookies.Keys);
        Assert.Contains("techyouth_refresh", cookies.Keys);
        Assert.Contains("techyouth_csrf", cookies.Keys);
        var setCookieHeaders = loginResponse.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(setCookieHeaders, value =>
            value.StartsWith("techyouth_access=", StringComparison.OrdinalIgnoreCase)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookieHeaders, value =>
            value.StartsWith("techyouth_refresh=", StringComparison.OrdinalIgnoreCase)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        using var refreshRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Post, "/api/auth/refresh", cookies);
        using var refreshResponse = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(refreshPayload.TryGetProperty("user", out _));
        Assert.False(refreshPayload.TryGetProperty("token", out _));
        Assert.False(refreshPayload.TryGetProperty("csrfToken", out _));
    }

    [Fact]
    public async Task Browser_One_Minute_Session_Expires_And_Remembered_Device_Refreshes()
    {
        using var factory = new ApiWebApplicationFactory(sessionDurationMinutes: 1);
        using var client = factory.CreateApiClient();
        var loginStartedAt = DateTime.UtcNow;
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/browser-login",
            new LoginRequest("admin", "admin123", RememberMe: true));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var expiresAt = loginPayload.GetProperty("expiresAt").GetDateTime();
        Assert.InRange(expiresAt, loginStartedAt.AddSeconds(45), DateTime.UtcNow.AddSeconds(75));
        var loginCookies = IntegrationTestHttp.ReadCookies(loginResponse);

        using var activeMeRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Get, "/api/auth/me", loginCookies);
        using var activeMeResponse = await client.SendAsync(activeMeRequest);
        Assert.Equal(HttpStatusCode.OK, activeMeResponse.StatusCode);

        await factory.ExecuteDbAsync(async db =>
        {
            var session = await db.UserSessions
                .Where(item => item.User!.Username == "admin" && item.RevokedAt == null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstAsync();
            session.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        });

        using var expiredMeRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Get, "/api/auth/me", loginCookies);
        using var expiredMeResponse = await client.SendAsync(expiredMeRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredMeResponse.StatusCode);

        using var refreshRequest = IntegrationTestHttp.CookieRequest(
            HttpMethod.Post,
            "/api/auth/refresh",
            new Dictionary<string, string> { ["techyouth_refresh"] = loginCookies["techyouth_refresh"] });
        using var refreshResponse = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(refreshPayload.TryGetProperty("token", out _));
        Assert.False(refreshPayload.TryGetProperty("csrfToken", out _));

        var rotatedCookies = IntegrationTestHttp.ReadCookies(refreshResponse);
        Assert.NotEqual(loginCookies["techyouth_access"], rotatedCookies["techyouth_access"]);
        Assert.NotEqual(loginCookies["techyouth_refresh"], rotatedCookies["techyouth_refresh"]);
        using var recoveredMeRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Get, "/api/auth/me", rotatedCookies);
        using var recoveredMeResponse = await client.SendAsync(recoveredMeRequest);
        Assert.Equal(HttpStatusCode.OK, recoveredMeResponse.StatusCode);
    }

    [Fact]
    public async Task Browser_One_Minute_Session_Without_RememberMe_Cannot_Refresh()
    {
        using var factory = new ApiWebApplicationFactory(sessionDurationMinutes: 1);
        using var client = factory.CreateApiClient();
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/browser-login",
            new LoginRequest("admin", "admin123", RememberMe: false));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var cookies = IntegrationTestHttp.ReadCookies(loginResponse);
        Assert.DoesNotContain("techyouth_refresh", cookies.Keys);

        await factory.ExecuteDbAsync(async db =>
        {
            var session = await db.UserSessions
                .Where(item => item.User!.Username == "admin" && item.RevokedAt == null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstAsync();
            session.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        });

        using var expiredMeRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Get, "/api/auth/me", cookies);
        using var expiredMeResponse = await client.SendAsync(expiredMeRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredMeResponse.StatusCode);

        using var refreshResponse = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Browser_Logout_With_Csrf_Revokes_Access_And_Refresh_Sessions()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/browser-login",
            new LoginRequest("admin", "admin123", RememberMe: true));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var cookies = IntegrationTestHttp.ReadCookies(loginResponse);

        using var logoutRequest = IntegrationTestHttp.CookieRequest(
            HttpMethod.Post,
            "/api/auth/logout",
            cookies,
            cookies["techyouth_csrf"]);
        using var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var meRequest = IntegrationTestHttp.CookieRequest(HttpMethod.Get, "/api/auth/me", cookies);
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

        using var refreshRequest = IntegrationTestHttp.CookieRequest(
            HttpMethod.Post,
            "/api/auth/refresh",
            new Dictionary<string, string> { ["techyouth_refresh"] = cookies["techyouth_refresh"] });
        using var refreshResponse = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
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
        Assert.False(refreshedPayload.TryGetProperty("token", out _));
        var rotatedCookies = IntegrationTestHttp.ReadCookies(refreshResponse);
        var refreshedAccessToken = rotatedCookies["techyouth_access"];
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
