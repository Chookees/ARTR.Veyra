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
}
