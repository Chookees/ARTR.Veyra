using ARTR.Veyra.Core.Routing;
using Microsoft.Extensions.Configuration;

namespace ARTR.Veyra.Core.Configuration;

/// <summary>
/// Validates ReverseProxy route metadata against Veyra routing security rules.
/// Lives in Core and only depends on <see cref="IConfiguration"/> abstractions.
/// </summary>
public static class ReverseProxyRouteSecurityValidator
{
    public static IReadOnlyList<string> Validate(
        IConfiguration configuration,
        VeyraOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (!options.Authentication.Enabled || !options.RoutingSecurity.DenyAnonymousRoutesByDefault)
        {
            ValidateRateLimitMetadata(configuration, options, failures);
            ValidateCanary(configuration, options, failures);
            ValidateClusterWeights(configuration, options, failures);
            return failures;
        }

        var routes = configuration.GetSection("ReverseProxy:Routes").GetChildren().ToArray();
        if (routes.Length == 0)
        {
            failures.Add(
                "Authentication is enabled with RoutingSecurity.DenyAnonymousRoutesByDefault, but no ReverseProxy:Routes are configured.");
            return failures;
        }

        var knownPolicies = new HashSet<string>(
            options.Authorization.Policies.Keys,
            StringComparer.OrdinalIgnoreCase)
        {
            "VeyraAdmin",
        };

        foreach (var route in routes)
        {
            var routeId = route.Key;
            var metadata = route.GetSection("Metadata");
            var allowAnonymous = ParseBool(metadata[VeyraRouteMetadata.AllowAnonymous]);
            var policy = metadata[VeyraRouteMetadata.AuthorizationPolicy];

            if (allowAnonymous)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(policy))
            {
                failures.Add(
                    $"ReverseProxy:Routes:{routeId} must set Metadata.{VeyraRouteMetadata.AllowAnonymous}=true or Metadata.{VeyraRouteMetadata.AuthorizationPolicy} when authentication deny-by-default is enabled.");
                continue;
            }

            if (!knownPolicies.Contains(policy) && options.Authorization.Enabled)
            {
                failures.Add(
                    $"ReverseProxy:Routes:{routeId} references unknown AuthorizationPolicy '{policy}'.");
            }
        }

        ValidateRateLimitMetadata(configuration, options, failures);
        ValidateCanary(configuration, options, failures);
        ValidateClusterWeights(configuration, options, failures);
        return failures;
    }

    private static void ValidateRateLimitMetadata(
        IConfiguration configuration,
        VeyraOptions options,
        List<string> failures)
    {
        if (!options.RateLimiting.Enabled)
        {
            return;
        }

        var policyNames = new HashSet<string>(
            options.RateLimiting.Policies.Select(static p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var route in configuration.GetSection("ReverseProxy:Routes").GetChildren())
        {
            var policy = route.GetSection("Metadata")[VeyraRouteMetadata.RateLimitPolicy];
            if (string.IsNullOrWhiteSpace(policy))
            {
                continue;
            }

            if (!policyNames.Contains(policy))
            {
                failures.Add(
                    $"ReverseProxy:Routes:{route.Key} references unknown RateLimitPolicy '{policy}'.");
            }
        }
    }

    private static void ValidateCanary(
        IConfiguration configuration,
        VeyraOptions options,
        List<string> failures)
    {
        if (!options.Canary.Enabled)
        {
            return;
        }

        if (options.Canary.Splits.Count == 0)
        {
            failures.Add("Canary.Enabled requires at least one split.");
            return;
        }

        var clusterIds = configuration.GetSection("ReverseProxy:Clusters").GetChildren()
            .Select(static c => c.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var split in options.Canary.Splits)
        {
            if (string.IsNullOrWhiteSpace(split.Name) || !seen.Add(split.Name))
            {
                failures.Add("Canary.Splits entries require unique non-empty Name values.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(split.ClusterId) || !clusterIds.Contains(split.ClusterId))
            {
                failures.Add($"Canary.Splits['{split.Name}'] references unknown ClusterId '{split.ClusterId}'.");
                continue;
            }

            if (split.Weights.Count == 0)
            {
                failures.Add($"Canary.Splits['{split.Name}'].Weights must not be empty.");
                continue;
            }

            var sum = 0;
            var destinations = configuration.GetSection($"ReverseProxy:Clusters:{split.ClusterId}:Destinations")
                .GetChildren()
                .Select(static d => d.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (destinationId, weight) in split.Weights)
            {
                if (!destinations.Contains(destinationId))
                {
                    failures.Add(
                        $"Canary.Splits['{split.Name}'].Weights references unknown destination '{destinationId}'.");
                }

                if (weight < 0 || weight > 100)
                {
                    failures.Add($"Canary.Splits['{split.Name}'].Weights['{destinationId}'] must be 0..100.");
                }

                sum += weight;
            }

            if (sum != 100)
            {
                failures.Add($"Canary.Splits['{split.Name}'].Weights must sum to 100 (actual {sum}).");
            }

            var hasHeaderName = !string.IsNullOrWhiteSpace(split.MatchHeaderName);
            var hasHeaderValue = !string.IsNullOrWhiteSpace(split.MatchHeaderValue);
            if (hasHeaderName != hasHeaderValue)
            {
                failures.Add(
                    $"Canary.Splits['{split.Name}'] requires both MatchHeaderName and MatchHeaderValue, or neither.");
            }
        }
    }

    private static void ValidateClusterWeights(
        IConfiguration configuration,
        VeyraOptions options,
        List<string> failures)
    {
        if (!options.TrafficEngineering.ValidateClusterWeights)
        {
            return;
        }

        foreach (var cluster in configuration.GetSection("ReverseProxy:Clusters").GetChildren())
        {
            var weights = new List<int>();
            foreach (var destination in cluster.GetSection("Destinations").GetChildren())
            {
                var raw = destination.GetSection("Metadata")["Weight"] ?? destination["Weight"];
                if (int.TryParse(raw, out var weight))
                {
                    if (weight < 0)
                    {
                        failures.Add(
                            $"ReverseProxy:Clusters:{cluster.Key}:Destinations:{destination.Key} weight cannot be negative.");
                    }

                    weights.Add(weight);
                }
            }

            if (weights.Count > 1 && weights.Sum() == 0)
            {
                failures.Add(
                    $"ReverseProxy:Clusters:{cluster.Key} has multiple destinations but all weights are zero.");
            }
        }
    }

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
