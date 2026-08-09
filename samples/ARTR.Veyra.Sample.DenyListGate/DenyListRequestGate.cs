using ARTR.Veyra.Core.Extensions;

namespace ARTR.Veyra.Sample.DenyListGate;

/// <summary>
/// Sample DI-only request gate. Register at host startup:
/// <code>
/// builder.Services.AddSingleton&lt;IVeyraRequestGate, DenyListRequestGate&gt;();
/// </code>
/// Enable via <c>ARTR:Veyra:Features:Enabled</c> containing <see cref="FeatureId"/>.
/// </summary>
public sealed class DenyListRequestGate : IVeyraRequestGate
{
    public const string FeatureIdValue = "deny-list";

    private static readonly HashSet<string> DeniedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/a/blocked",
        "/b/blocked",
    };

    public string FeatureId => FeatureIdValue;

    public ValueTask<VeyraGateResult> EvaluateAsync(VeyraGateContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (DeniedPaths.Contains(context.Path))
        {
            return ValueTask.FromResult(
                VeyraGateResult.Deny(StatusCodes.Status403Forbidden, "Path is on the sample deny-list."));
        }

        return ValueTask.FromResult(VeyraGateResult.Allow());
    }
}

internal static class StatusCodes
{
    public const int Status403Forbidden = 403;
}
