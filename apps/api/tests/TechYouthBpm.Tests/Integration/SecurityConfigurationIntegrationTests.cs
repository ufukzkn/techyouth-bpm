using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Api.Configuration;

namespace TechYouthBpm.Tests.Integration;

public sealed class SecurityConfigurationIntegrationTests
{
    [Fact]
    public async Task Configured_origin_receives_credential_cors_headers()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "http://localhost:3000");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "http://localhost:3000",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task Unknown_origin_does_not_receive_cors_access()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "https://untrusted.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void Wildcard_origin_is_rejected_when_credentials_are_enabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "*",
                ["Frontend:BaseUrl"] = null,
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => WebCorsConfiguration.GetAllowedOrigins(configuration));

        Assert.Contains("Wildcard CORS origins", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_https_response_includes_hsts()
    {
        using var factory = new ApiWebApplicationFactory(environment: "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.techyouth.test"),
            HandleCookies = false,
        });

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Trusted_forwarded_proto_marks_browser_session_cookies_secure()
    {
        using var factory = new ApiWebApplicationFactory(
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Proxy:TrustForwardedProto"] = "true",
            },
            environment: "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.techyouth.test"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/browser-login")
        {
            Content = JsonContent.Create(new LoginRequest("admin", "admin123", RememberMe: true)),
        };
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.True(setCookieHeaders.Any(value =>
            value.StartsWith("techyouth_access=", StringComparison.OrdinalIgnoreCase)
            && value.Contains("secure", StringComparison.OrdinalIgnoreCase)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, setCookieHeaders));
        Assert.True(setCookieHeaders.Any(value =>
            value.StartsWith("techyouth_refresh=", StringComparison.OrdinalIgnoreCase)
            && value.Contains("secure", StringComparison.OrdinalIgnoreCase)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, setCookieHeaders));
        Assert.True(setCookieHeaders.Any(value =>
            value.StartsWith("techyouth_csrf=", StringComparison.OrdinalIgnoreCase)
            && value.Contains("secure", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, setCookieHeaders));
    }
}
