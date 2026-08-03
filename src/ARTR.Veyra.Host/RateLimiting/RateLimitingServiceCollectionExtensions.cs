using System.Threading.RateLimiting;
using ARTR.Veyra.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Host.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public const string GlobalPolicyName = "veyra-global";

    public static IServiceCollection AddVeyraRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var startupPolicies = configuration.GetSection(VeyraOptions.SectionName).Get<VeyraOptions>()?.RateLimiting.Policies
            ?? [];

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://tools.ietf.org/html/rfc6585#section-4",
                        title = "Too Many Requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Rate limit exceeded.",
                        errorCode = Core.Errors.VeyraErrorCodes.RateLimited,
                    },
                    token).ConfigureAwait(false);
            };

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var rateOptions = httpContext.RequestServices
                    .GetRequiredService<IOptionsMonitor<VeyraOptions>>()
                    .CurrentValue.RateLimiting;

                if (!rateOptions.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateOptions.GlobalPermitLimit,
                        Window = TimeSpan.FromSeconds(rateOptions.GlobalWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });

            foreach (var policy in startupPolicies)
            {
                if (string.IsNullOrWhiteSpace(policy.Name))
                {
                    continue;
                }

                var policyName = policy.Name;
                limiter.AddPolicy(policyName, httpContext =>
                {
                    var rateOptions = httpContext.RequestServices
                        .GetRequiredService<IOptionsMonitor<VeyraOptions>>()
                        .CurrentValue.RateLimiting;

                    var configuredPolicy = rateOptions.Policies
                        .FirstOrDefault(p => string.Equals(p.Name, policyName, StringComparison.OrdinalIgnoreCase));

                    if (configuredPolicy is null)
                    {
                        return RateLimitPartition.GetNoLimiter(policyName);
                    }

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = configuredPolicy.PermitLimit,
                            Window = TimeSpan.FromSeconds(configuredPolicy.WindowSeconds),
                            QueueLimit = configuredPolicy.QueueLimit,
                            AutoReplenishment = true,
                        });
                });
            }
        });

        return services;
    }
}
