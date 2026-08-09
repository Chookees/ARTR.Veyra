using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ARTR.Veyra.Observability.Telemetry;

public static class VeyraInstrumentation
{
    public const string ActivitySourceName = "ARTR.Veyra";

    public const string MeterName = "ARTR.Veyra";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RequestsTotal =
        Meter.CreateCounter<long>("veyra.requests.total", description: "Total number of gateway requests.");

    public static readonly Counter<long> AuthFailuresTotal =
        Meter.CreateCounter<long>("veyra.auth.failures.total", description: "Total number of authentication failures.");

    public static readonly Counter<long> RateLimitExceededTotal =
        Meter.CreateCounter<long>(
            "veyra.ratelimit.exceeded.total",
            description: "Total number of requests rejected by rate limiting.");

    public static readonly Counter<long> ProxyErrorsTotal =
        Meter.CreateCounter<long>("veyra.proxy.errors.total", description: "Total number of reverse proxy errors.");

    public static readonly ObservableGauge<long> HealthyDestinations =
        Meter.CreateObservableGauge(
            "veyra.destinations.healthy",
            static () => DestinationHealthSnapshot.Healthy,
            description: "Count of destinations currently considered healthy.");

    public static readonly ObservableGauge<long> EjectedDestinations =
        Meter.CreateObservableGauge(
            "veyra.destinations.ejected",
            static () => DestinationHealthSnapshot.Ejected,
            description: "Count of destinations currently ejected by passive health.");
}

/// <summary>Latest destination health counts updated by the Host metrics publisher.</summary>
public static class DestinationHealthSnapshot
{
    private static long _healthy;
    private static long _ejected;

    public static long Healthy => Interlocked.Read(ref _healthy);

    public static long Ejected => Interlocked.Read(ref _ejected);

    public static void Publish(long healthy, long ejected)
    {
        Interlocked.Exchange(ref _healthy, healthy);
        Interlocked.Exchange(ref _ejected, ejected);
    }
}
