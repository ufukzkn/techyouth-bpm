using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechYouthBpm.Api.Observability;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Dashboard;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Tests.Integration;

public sealed class ObservabilityIntegrationTests
{
    [Fact]
    public async Task Valid_correlation_id_is_echoed_on_the_response()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "demo-request_2026.07");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "demo-request_2026.07",
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Fact]
    public async Task Invalid_correlation_id_is_replaced_with_a_safe_generated_value()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, new string('x', 65));

        using var response = await client.SendAsync(request);

        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.Equal(32, correlationId.Length);
        Assert.True(correlationId.All(char.IsAsciiHexDigit));
    }

    [Fact]
    public async Task Unexpected_exception_returns_safe_problem_details_without_internal_data()
    {
        using var factory = new ApiWebApplicationFactory(configureServices: services =>
        {
            services.RemoveAll<IDashboardService>();
            services.AddScoped<IDashboardService, ThrowingDashboardService>();
        });
        using var client = factory.CreateApiClient();
        var (session, _) = await IntegrationTestHttp.LoginAsync(client);
        using var request = IntegrationTestHttp.BearerRequest(
            HttpMethod.Get,
            "/api/dashboard/summary",
            session.Token);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "failing-dashboard-request");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, payload.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "failing-dashboard-request",
            payload.RootElement.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(ThrowingDashboardService.SensitiveMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingDashboardService : IDashboardService
    {
        public const string SensitiveMessage = "private database failure with secret details";

        public Task<DashboardSummaryDto> GetSummaryAsync(
            UserDto user,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SensitiveMessage);

        public Task<DashboardSummaryDto> GetSummaryAsync(
            UserDto user,
            WorkflowVisibilityScope scope,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SensitiveMessage);
    }
}
