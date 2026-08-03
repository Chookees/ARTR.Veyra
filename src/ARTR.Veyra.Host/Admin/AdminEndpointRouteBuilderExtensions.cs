using System.Reflection;
using ARTR.Veyra.Authentication.Authorization;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ARTR.Veyra.Host.Admin;

public static class AdminEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapVeyraAdmin(
        this IEndpointRouteBuilder endpoints,
        string adminPath,
        VeyraOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        var group = endpoints.MapGroup(adminPath);
        if (options.Admin.RequireAuthentication && options.Authentication.Enabled)
        {
            group.RequireAuthorization(VeyraAuthorizationExtensions.VeyraAdminPolicyName);
        }
        else
        {
            group.AllowAnonymous();
        }

        group.MapGet("/info", () =>
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "0.0.0";

            return Results.Json(new
            {
                product = VeyraConstants.ProductName,
                tagline = VeyraConstants.Tagline,
                version,
                adminPath = options.Admin.PathBase,
                authenticationEnabled = options.Authentication.Enabled,
                rateLimitingEnabled = options.RateLimiting.Enabled,
                prometheusEnabled = options.Observability.Prometheus.Enabled,
            });
        });

        group.MapGet("/config/summary", (
            Microsoft.Extensions.Options.IOptions<VeyraOptions> opts,
            ARTR.Veyra.Infrastructure.Configuration.IConfigurationActivationState activation) =>
        {
            var value = opts.Value;
            return Results.Json(new
            {
                admin = new
                {
                    value.Admin.Enabled,
                    value.Admin.PathBase,
                    value.Admin.RequireAuthentication,
                    value.Admin.ListenUrls,
                },
                authentication = new
                {
                    value.Authentication.Enabled,
                    jwtEnabled = value.Authentication.Jwt.Enabled,
                    apiKeyEnabled = value.Authentication.ApiKey.Enabled,
                    apiKeyCount = value.Authentication.ApiKey.Keys.Count,
                },
                rateLimiting = new
                {
                    value.RateLimiting.Enabled,
                    value.RateLimiting.GlobalPermitLimit,
                    value.RateLimiting.GlobalWindowSeconds,
                    policyCount = value.RateLimiting.Policies.Count,
                },
                observability = new
                {
                    value.Observability.ServiceName,
                    otlpEnabled = value.Observability.Otlp.Enabled,
                    prometheusEnabled = value.Observability.Prometheus.Enabled,
                },
                configuration = new
                {
                    generation = activation.Generation,
                    fingerprint = activation.Fingerprint,
                    lastKnownGoodActive = activation.IsLastKnownGoodActive,
                    lastActivatedUtc = activation.LastActivatedUtc,
                },
            });
        });

        return endpoints;
    }
}
