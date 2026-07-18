using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TechYouthBpm.Api;
using TechYouthBpm.Infrastructure;
using TechYouthBpm.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Web");
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedMockData = app.Configuration.GetValue("Seed:MockData", true);
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db, seedMockData);
}

app.Run();

public partial class Program;
