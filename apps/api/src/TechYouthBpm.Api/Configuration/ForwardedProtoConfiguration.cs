using Microsoft.AspNetCore.HttpOverrides;

namespace TechYouthBpm.Api.Configuration;

public static class ForwardedProtoConfiguration
{
    public static IServiceCollection AddConfiguredForwardedProto(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            if (!configuration.GetValue("Proxy:TrustForwardedProto", false))
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
