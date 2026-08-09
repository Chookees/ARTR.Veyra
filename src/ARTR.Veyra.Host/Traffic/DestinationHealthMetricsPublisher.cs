using ARTR.Veyra.Observability.Telemetry;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;

namespace ARTR.Veyra.Host.Traffic;

/// <summary>
/// Publishes destination healthy / ejected gauges from YARP proxy state.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class DestinationHealthMetricsPublisher : BackgroundService
{
    private readonly IProxyStateLookup _proxyState;

    public DestinationHealthMetricsPublisher(IProxyStateLookup proxyState)
    {
        _proxyState = proxyState ?? throw new ArgumentNullException(nameof(proxyState));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                long healthy = 0;
                long ejected = 0;

                foreach (var cluster in _proxyState.GetClusters())
                {
                    foreach (var destination in cluster.Destinations.Values)
                    {
                        var passive = destination.Health.Passive;
                        if (passive == DestinationHealth.Unhealthy)
                        {
                            ejected++;
                        }
                        else
                        {
                            healthy++;
                        }
                    }
                }

                DestinationHealthSnapshot.Publish(healthy, ejected);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                // Best-effort metrics; never break the host.
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
