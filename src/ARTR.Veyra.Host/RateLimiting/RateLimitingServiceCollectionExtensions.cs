using System.Threading.RateLimiting;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Errors;
using ARTR.Veyra.Core.RateLimiting;
using ARTR.Veyra.Core.Routing;
using ARTR.Veyra.Observability.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;

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
                VeyraInstrumentation.RateLimitExceededTotal.Add(1);
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://tools.ietf.org/html/rfc6585#section-4",
                        title = "Too Many Requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Rate limit exceeded.",
                        errorCode = VeyraErrorCodes.RateLimited,
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

    /// <summary>
    /// Applies named rate-limit policies from YARP route metadata using <see cref="IRateLimiterStore"/>.
    /// </summary>
    public static IApplicationBuilder UseVeyraRouteRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptionsMonitor<VeyraOptions>>().CurrentValue;
            if (!options.RateLimiting.Enabled)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var proxyFeature = context.Features.Get<IReverseProxyFeature>();
            var metadata = proxyFeature?.Route.Config.Metadata;
            if (metadata is null ||
                !metadata.TryGetValue(VeyraRouteMetadata.RateLimitPolicy, out var policyName) ||
                string.IsNullOrWhiteSpace(policyName))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var policy = options.RateLimiting.Policies
                .FirstOrDefault(p => string.Equals(p.Name, policyName, StringComparison.OrdinalIgnoreCase));
            if (policy is null)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var store = context.RequestServices.GetRequiredService<IRateLimiterStore>();
            var partition = $"{policy.Name}:{context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"}";
            var acquired = await store.TryAcquireAsync(
                partition,
                policy.PermitLimit,
                TimeSpan.FromSeconds(policy.WindowSeconds),
                context.RequestAborted).ConfigureAwait(false);

            if (acquired)
            {
                await next().ConfigureAwait(false);
                return;
            }

            VeyraInstrumentation.RateLimitExceededTotal.Add(1);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = $"Rate limit policy '{policy.Name}' exceeded.",
                    errorCode = VeyraErrorCodes.RateLimited,
                },
                context.RequestAborted).ConfigureAwait(false);
        });
    }
}
