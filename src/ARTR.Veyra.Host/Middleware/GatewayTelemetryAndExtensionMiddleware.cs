using System.Diagnostics;
using ARTR.Veyra.Core.Errors;
using ARTR.Veyra.Core.Extensions;
using ARTR.Veyra.Core.Routing;
using ARTR.Veyra.Observability.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Model;

namespace ARTR.Veyra.Host.Middleware;

public static class GatewayTelemetryAndExtensionMiddleware
{
    public static IApplicationBuilder UseVeyraGatewayTelemetry(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            VeyraInstrumentation.RequestsTotal.Add(1);

            try
            {
                await next().ConfigureAwait(false);
            }
            finally
            {
                var proxyFeature = context.Features.Get<IReverseProxyFeature>();
                var status = context.Response.StatusCode;
                var proxyError = status >= 500 || status == StatusCodes.Status502BadGateway ||
                                 status == StatusCodes.Status503ServiceUnavailable ||
                                 status == StatusCodes.Status504GatewayTimeout;
                if (proxyError)
                {
                    VeyraInstrumentation.ProxyErrorsTotal.Add(1);
                }

                if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
                {
                    VeyraInstrumentation.AuthFailuresTotal.Add(1);
                }

                // 429 is counted at the rate-limiter source to avoid double-counting.

                var observers = context.RequestServices.GetServices<IVeyraProxyObserver>();
                var observation = new VeyraProxyObservation
                {
                    RouteId = proxyFeature?.Route.Config.RouteId,
                    ClusterId = proxyFeature?.Cluster?.Config.ClusterId,
                    StatusCode = status,
                    ProxyError = proxyError,
                    Duration = Stopwatch.GetElapsedTime(started),
                };

                foreach (var observer in observers)
                {
                    await observer.OnProxiedAsync(observation, context.RequestAborted).ConfigureAwait(false);
                }
            }
        });
    }

    public static IApplicationBuilder UseVeyraRequestGates(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var features = context.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ARTR.Veyra.Core.Configuration.VeyraOptions>>()
                .CurrentValue.Features.Enabled;
            var enabledSet = features.Count == 0
                ? null
                : new HashSet<string>(features, StringComparer.OrdinalIgnoreCase);

            var gates = context.RequestServices.GetServices<IVeyraRequestGate>()
                .Where(gate => enabledSet is null || enabledSet.Contains(gate.FeatureId))
                .ToArray();
            if (gates.Length == 0)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var proxyFeature = context.Features.Get<IReverseProxyFeature>();
            var metadata = proxyFeature?.Route.Config.Metadata
                ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in context.Request.Headers)
            {
                if (headers.Count >= 32)
                {
                    break;
                }

                var name = header.Key;
                if (name.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("api-key", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                headers[name] = header.Value.ToString().Length > 128
                    ? header.Value.ToString()[..128]
                    : header.Value.ToString();
            }

            var gateContext = new VeyraGateContext
            {
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? "/",
                RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                IsAuthenticated = context.User.Identity?.IsAuthenticated == true,
                RouteId = proxyFeature?.Route.Config.RouteId,
                ClusterId = proxyFeature?.Cluster?.Config.ClusterId,
                RouteMetadata = metadata,
                Headers = headers,
            };

            foreach (var gate in gates)
            {
                var result = await gate.EvaluateAsync(gateContext, context.RequestAborted).ConfigureAwait(false);
                if (result.Allowed)
                {
                    continue;
                }

                var status = result.StatusCode ?? StatusCodes.Status403Forbidden;
                var problem = new ProblemDetails
                {
                    Title = "Forbidden",
                    Status = status,
                    Detail = result.Detail ?? "Request denied by gateway extension gate.",
                    Instance = context.Request.Path,
                    Extensions =
                    {
                        ["errorCode"] = VeyraErrorCodes.Forbidden,
                        ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
                        ["featureId"] = gate.FeatureId,
                    },
                };
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json")
                    .ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });
    }
}
