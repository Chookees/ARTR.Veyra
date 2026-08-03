using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Observability.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

namespace ARTR.Veyra.Observability.DependencyInjection;

public static class ObservabilityApplicationBuilderExtensions
{
    public static IApplicationBuilder UseVeyraCorrelation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    public static WebApplication MapVeyraPrometheusScrapingEndpoint(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var veyraOptions = app.Services.GetRequiredService<IOptions<VeyraOptions>>().Value;
        if (!veyraOptions.Observability.Prometheus.Enabled)
        {
            return app;
        }

        var adminBase = veyraOptions.Admin.PathBase.TrimEnd('/');
        var metricsPath = veyraOptions.Observability.Prometheus.Path;
        if (!metricsPath.StartsWith('/'))
        {
            metricsPath = "/" + metricsPath;
        }

        var fullPath = adminBase + metricsPath;
        app.MapPrometheusScrapingEndpoint(fullPath);

        return app;
    }
}
