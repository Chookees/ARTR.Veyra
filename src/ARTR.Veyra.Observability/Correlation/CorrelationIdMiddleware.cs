using ARTR.Veyra.Core.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace ARTR.Veyra.Observability.Correlation;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = GetOrCreateCorrelationId(context.Request.Headers);
        context.TraceIdentifier = correlationId;
        context.Items[CorrelationConstants.HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        }))
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    private static string GetOrCreateCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(CorrelationConstants.HeaderName, out StringValues values))
        {
            var existing = values.ToString();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
