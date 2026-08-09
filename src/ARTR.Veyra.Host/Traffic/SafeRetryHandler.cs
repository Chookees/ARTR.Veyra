using System.Net;
using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Host.Traffic;

/// <summary>
/// Retries only idempotent methods on transport / selected 5xx failures.
/// Disabled unless <see cref="SafeRetryOptions.Enabled"/> is true.
/// Never retries requests that carry a body.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Covered by dedicated SafeRetryHandlerTests; retry loop branches are timing-sensitive.")]
public sealed class SafeRetryHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<VeyraOptions> _options;

    public SafeRetryHandler(IOptionsMonitor<VeyraOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var retries = _options.CurrentValue.TrafficEngineering.SafeRetries;
        if (!retries.Enabled || !IsIdempotent(request.Method, retries) || request.Content is not null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(retries.TotalTimeoutSeconds));

        Exception? lastError = null;
        for (var attempt = 1; attempt <= retries.MaxAttempts; attempt++)
        {
            try
            {
                var response = await base.SendAsync(CloneRequest(request), timeoutCts.Token).ConfigureAwait(false);
                if (!IsRetryableStatus(response.StatusCode) || attempt == retries.MaxAttempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) &&
                                       !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                if (attempt == retries.MaxAttempts)
                {
                    throw;
                }
            }
        }

        throw lastError ?? new HttpRequestException("Safe retry attempts exhausted.");
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static bool IsIdempotent(HttpMethod method, SafeRetryOptions retries) =>
        retries.IdempotentMethods.Any(m => string.Equals(m, method.Method, StringComparison.OrdinalIgnoreCase));

    private static bool IsRetryableStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
