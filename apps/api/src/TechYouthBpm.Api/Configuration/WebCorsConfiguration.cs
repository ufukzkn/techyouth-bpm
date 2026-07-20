namespace TechYouthBpm.Api.Configuration;

public static class WebCorsConfiguration
{
    public const string PolicyName = "Web";

    public static IServiceCollection AddConfiguredWebCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = GetAllowedOrigins(configuration);
        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static string[] GetAllowedOrigins(IConfiguration configuration)
    {
        var configuredOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        var fallbackOrigin = configuration["Frontend:BaseUrl"];
        var origins = configuredOrigins
            .Append(fallbackOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin!.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins.Length == 0)
        {
            throw new InvalidOperationException("At least one CORS origin must be configured.");
        }

        if (origins.Any(origin => origin == "*"))
        {
            throw new InvalidOperationException(
                "Wildcard CORS origins cannot be used when credential cookies are enabled.");
        }

        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException($"CORS origin '{origin}' must be an HTTP(S) origin without a path.");
            }
        }

        return origins;
    }
}
