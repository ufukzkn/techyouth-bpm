using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using TechYouthBpm.Api;
using TechYouthBpm.Api.Configuration;
using TechYouthBpm.Api.Health;
using TechYouthBpm.Api.Observability;
using TechYouthBpm.Infrastructure;
using TechYouthBpm.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
    });
}

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(180);
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["traceId"] = traceId;
        context.ProblemDetails.Extensions["correlationId"] =
            CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);

        if (context.ProblemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Title = "An unexpected error occurred.";
            context.ProblemDetails.Detail = "The request could not be completed.";
        }
    };
});
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<SystemReadinessHealthCheck>("system", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("Auth:RateLimitPermitLimit", 10),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("Auth:RateLimitWindowMinutes", 1)),
                QueueLimit = 0
            }));
});
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Token",
        In = ParameterLocation.Header,
        Description = "Paste the token returned from POST /api/auth/login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddConfiguredWebCors(builder.Configuration);
builder.Services.AddConfiguredForwardedProto(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SafeRequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseCors(WebCorsConfiguration.PolicyName);
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var isApiMutation = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method));
    var isPublicAuthEndpoint = context.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/auth/browser-login", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/auth/register", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/api/auth/public-email-verification", StringComparison.OrdinalIgnoreCase);
    var hasBearerHeader = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    if (isApiMutation
        && !isPublicAuthEndpoint
        && !hasBearerHeader
        && context.Request.Cookies.ContainsKey(AuthCookieNames.AccessToken))
    {
        var csrfCookie = context.Request.Cookies[AuthCookieNames.CsrfToken];
        var csrfHeader = context.Request.Headers[AuthCookieNames.CsrfHeader].ToString();
        if (string.IsNullOrWhiteSpace(csrfCookie)
            || string.IsNullOrWhiteSpace(csrfHeader)
            || !string.Equals(csrfCookie, csrfHeader, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { errors = new[] { "CSRF token is invalid." } });
            return;
        }
    }

    await next();
});
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedMockData = app.Configuration.GetValue("Seed:MockData", true);
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db, seedMockData);
}

app.Run();

public partial class Program;
