using ARTR.Veyra.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ARTR.Veyra.Host.Health;

public static class HealthExtensions
{
    public static IServiceCollection AddVeyraHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("ARTR Veyra is running."), tags: ["live", "ready", "startup"]);
        return services;
    }

    public static IEndpointRouteBuilder MapVeyraHealth(
        this IEndpointRouteBuilder endpoints,
        string adminPath,
        VeyraOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        endpoints.MapHealthChecks(adminPath + options.Health.LivePath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        }).AllowAnonymous();

        endpoints.MapHealthChecks(adminPath + options.Health.ReadyPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        }).AllowAnonymous();

        endpoints.MapHealthChecks(adminPath + options.Health.StartupPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
        }).AllowAnonymous();

        return endpoints;
    }
}
