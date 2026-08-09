namespace ARTR.Veyra.Core.Extensions;

/// <summary>
/// Pre-proxy gate that can allow or deny a request after routing metadata is known.
/// Registered only via DI at host startup (no dynamic untrusted loading in v1.x).
/// </summary>
public interface IVeyraRequestGate
{
    string FeatureId { get; }

    ValueTask<VeyraGateResult> EvaluateAsync(VeyraGateContext context, CancellationToken cancellationToken = default);
}

public sealed class VeyraGateContext
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    public string? RemoteIp { get; init; }

    public bool IsAuthenticated { get; init; }

    public string? RouteId { get; init; }

    public string? ClusterId { get; init; }

    public IReadOnlyDictionary<string, string> RouteMetadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class VeyraGateResult
{
    public bool Allowed { get; private init; }

    public int? StatusCode { get; private init; }

    public string? Detail { get; private init; }

    public static VeyraGateResult Allow() => new() { Allowed = true };

    public static VeyraGateResult Deny(int statusCode, string detail) => new()
    {
        Allowed = false,
        StatusCode = statusCode,
        Detail = detail,
    };
}
