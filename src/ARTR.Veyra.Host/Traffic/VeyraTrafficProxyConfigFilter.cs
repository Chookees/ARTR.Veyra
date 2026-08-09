using System.Globalization;
using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace ARTR.Veyra.Host.Traffic;

/// <summary>
/// Applies canary destination weights and outlier passive-health defaults to YARP clusters.
/// </summary>
public sealed class VeyraTrafficProxyConfigFilter : IProxyConfigFilter
{
    private readonly IOptionsMonitor<VeyraOptions> _options;

    public VeyraTrafficProxyConfigFilter(IOptionsMonitor<VeyraOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        var options = _options.CurrentValue;
        var destinations = cluster.Destinations is null
            ? null
            : new Dictionary<string, DestinationConfig>(cluster.Destinations, StringComparer.OrdinalIgnoreCase);

        if (options.Canary.Enabled && destinations is not null)
        {
            var split = options.Canary.Splits.FirstOrDefault(s =>
                string.Equals(s.ClusterId, cluster.ClusterId, StringComparison.OrdinalIgnoreCase));
            if (split is not null)
            {
                var updated = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);
                foreach (var (id, destination) in destinations)
                {
                    if (!split.Weights.TryGetValue(id, out var weight))
                    {
                        updated[id] = destination;
                        continue;
                    }

                    var metadata = destination.Metadata is null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(destination.Metadata, StringComparer.OrdinalIgnoreCase);
                    metadata["Weight"] = weight.ToString(CultureInfo.InvariantCulture);
                    updated[id] = destination with { Metadata = metadata };
                }

                destinations = updated;
            }
        }

        var health = cluster.HealthCheck;
        if (options.TrafficEngineering.OutlierDetection.Enabled)
        {
            var outlier = options.TrafficEngineering.OutlierDetection;
            var passive = health?.Passive ?? new PassiveHealthCheckConfig();
            passive = passive with
            {
                Enabled = true,
                Policy = string.IsNullOrWhiteSpace(passive.Policy) ? "TransportFailureRate" : passive.Policy,
                ReactivationPeriod = TimeSpan.FromSeconds(outlier.EjectionDurationSeconds),
            };
            health = (health ?? new HealthCheckConfig()) with { Passive = passive };
        }

        var updatedCluster = cluster with
        {
            Destinations = destinations,
            HealthCheck = health,
            LoadBalancingPolicy = string.IsNullOrWhiteSpace(cluster.LoadBalancingPolicy)
                ? "PowerOfTwoChoices"
                : cluster.LoadBalancingPolicy,
        };

        return ValueTask.FromResult(updatedCluster);
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(
        RouteConfig route,
        ClusterConfig? cluster,
        CancellationToken cancel) =>
        ValueTask.FromResult(route);
}
