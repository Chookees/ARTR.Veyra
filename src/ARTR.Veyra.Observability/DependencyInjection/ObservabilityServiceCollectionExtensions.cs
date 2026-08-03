using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Observability.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ARTR.Veyra.Observability.DependencyInjection;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddVeyraObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(VeyraOptions.SectionName).Get<VeyraOptions>()?.Observability
            ?? new ObservabilityOptions();

        _ = VeyraInstrumentation.ActivitySource;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(VeyraInstrumentation.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (options.Otlp.Enabled)
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.Otlp.Endpoint))
                        {
                            otlp.Endpoint = new Uri(options.Otlp.Endpoint);
                        }
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(VeyraInstrumentation.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (options.Otlp.Enabled)
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.Otlp.Endpoint))
                        {
                            otlp.Endpoint = new Uri(options.Otlp.Endpoint);
                        }
                    });
                }

                if (options.Prometheus.Enabled)
                {
                    metrics.AddPrometheusExporter();
                }
            });

        return services;
    }
}
