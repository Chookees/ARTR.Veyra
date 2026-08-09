using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Forwarder;

namespace ARTR.Veyra.Host.Traffic;

/// <summary>
/// YARP forwarder client factory that optionally wraps outbound calls with <see cref="SafeRetryHandler"/>.
/// Preserves YARP defaults for TLS / certificates; never disables remote certificate validation.
/// </summary>
public sealed class SafeRetryForwarderHttpClientFactory : ForwarderHttpClientFactory
{
    private readonly IOptionsMonitor<VeyraOptions> _options;

    public SafeRetryForwarderHttpClientFactory(IOptionsMonitor<VeyraOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_options.CurrentValue.TrafficEngineering.SafeRetries.Enabled)
        {
            return handler;
        }

        return new SafeRetryHandler(_options)
        {
            InnerHandler = handler,
        };
    }
}
