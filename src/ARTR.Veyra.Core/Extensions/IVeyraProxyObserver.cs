namespace ARTR.Veyra.Core.Extensions;

/// <summary>
/// Post-proxy observer for metrics and audit hooks. Must not mutate the response body.
/// </summary>
public interface IVeyraProxyObserver
{
    string FeatureId { get; }

    ValueTask OnProxiedAsync(VeyraProxyObservation observation, CancellationToken cancellationToken = default);
}

public sealed class VeyraProxyObservation
{
    public string? RouteId { get; init; }

    public string? ClusterId { get; init; }

    public int StatusCode { get; init; }

    public bool ProxyError { get; init; }

    public TimeSpan Duration { get; init; }
}
