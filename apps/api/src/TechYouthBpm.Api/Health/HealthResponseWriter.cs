using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TechYouthBpm.Api.Health;

public static class HealthResponseWriter
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache";

        var checks = report.Entries
            .SelectMany(entry => entry.Value.Data.Count == 0
                ? [new HealthCheckResponse(entry.Key, entry.Value.Status.ToString())]
                : entry.Value.Data.Select(item => new HealthCheckResponse(
                    item.Key,
                    item.Value is true ? HealthStatus.Healthy.ToString() : HealthStatus.Unhealthy.ToString())))
            .OrderBy(check => check.Name, StringComparer.Ordinal)
            .ToArray();

        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks,
            traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        });
    }

    private sealed record HealthCheckResponse(string Name, string Status);
}
