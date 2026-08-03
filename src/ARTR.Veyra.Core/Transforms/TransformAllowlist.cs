using System.Collections.Frozen;

namespace ARTR.Veyra.Core.Transforms;

public static class TransformAllowlist
{
    public static IReadOnlySet<string> DefaultAllowlist { get; } = CreateDefaultAllowlist();

    private static readonly FrozenSet<string> KnownParameterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Set",
        "Append",
        "When",
        "Mode",
        "Prefix",
        "Query",
        "Value",
        "AppendSeparator",
        "Match",
        "Replace",
        "Replacement",
        "Unsafe",
        "Name",
        "From",
        "To",
        "Action",
        "Header",
        "Destination",
        "Source",
        "Format",
        "Default",
        "Remove",
        "Copy",
        "Keep",
        "Order",
        "Separator",
        "Transform",
        "RouteValue",
        "QueryParameter",
        "Path",
        "Host",
        "Proto",
        "For",
        "By",
        "Port",
        "Scheme",
        "Certificate",
        "Trailers",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static TransformAllowlistValidationResult Validate(
        IEnumerable<IReadOnlyDictionary<string, object?>> transforms,
        IReadOnlySet<string>? allowlist = null)
    {
        ArgumentNullException.ThrowIfNull(transforms);

        var effectiveAllowlist = allowlist ?? DefaultAllowlist;
        var errors = new List<string>();
        var index = 0;

        foreach (var transform in transforms)
        {
            if (transform is null)
            {
                errors.Add($"Transform at index {index} is null.");
                index++;
                continue;
            }

            var transformKeys = transform.Keys
                .Where(key => effectiveAllowlist.Contains(key))
                .ToArray();

            if (transformKeys.Length == 0)
            {
                errors.Add($"Transform at index {index} does not contain a recognized transform key.");
            }
            else if (transformKeys.Length > 1)
            {
                errors.Add(
                    $"Transform at index {index} contains multiple transform keys: {string.Join(", ", transformKeys)}.");
            }

            foreach (var key in transform.Keys)
            {
                if (effectiveAllowlist.Contains(key) || KnownParameterKeys.Contains(key))
                {
                    continue;
                }

                errors.Add($"Transform at index {index} contains disallowed key '{key}'.");
            }

            index++;
        }

        return errors.Count == 0
            ? TransformAllowlistValidationResult.Success
            : new TransformAllowlistValidationResult(false, errors);
    }

    private static FrozenSet<string> CreateDefaultAllowlist()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PathPrefix",
            "PathRemovePrefix",
            "PathSet",
            "PathPattern",
            "PathRouteValue",
            "RequestHeader",
            "RequestHeaderRemove",
            "RequestHeaderRouteValue",
            "RequestHeaderOriginalHost",
            "ResponseHeader",
            "ResponseHeaderRemove",
            "HttpMethodChange",
            "QueryValueParameter",
            "QueryRemoveParameter",
            "QueryRouteParameter",
            "RequestHeadersCopy",
            "ResponseHeadersCopy",
            "RequestTrailersCopy",
            "ResponseTrailersCopy",
            "X-Forwarded",
            "Forwarded",
            "RouteValue",
            "ClientCert",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record TransformAllowlistValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static TransformAllowlistValidationResult Success { get; } = new(true, Array.Empty<string>());
}
