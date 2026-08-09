using System.Diagnostics;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Errors;
using ARTR.Veyra.Core.Routing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;

namespace ARTR.Veyra.Host.Middleware;

public static class ReverseProxyAuthorizationMiddleware
{
    public static IApplicationBuilder UseVeyraReverseProxyAuthorization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptionsMonitor<VeyraOptions>>().CurrentValue;
            if (!options.Authentication.Enabled)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var proxyFeature = context.Features.Get<IReverseProxyFeature>();
            var metadata = proxyFeature?.Route.Config.Metadata
                ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

            if (IsTruthy(metadata, VeyraRouteMetadata.AllowAnonymous))
            {
                await next().ConfigureAwait(false);
                return;
            }

            metadata.TryGetValue(VeyraRouteMetadata.AuthorizationPolicy, out var policyName);
            if (string.IsNullOrWhiteSpace(policyName))
            {
                if (options.RoutingSecurity.DenyAnonymousRoutesByDefault)
                {
                    await WriteProblemAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        "This route is not configured for anonymous access.",
                        VeyraErrorCodes.Forbidden).ConfigureAwait(false);
                    return;
                }

                await next().ConfigureAwait(false);
                return;
            }

            var authService = context.RequestServices.GetRequiredService<IAuthorizationService>();
            var result = await authService.AuthorizeAsync(context.User, resource: null, policyName)
                .ConfigureAwait(false);
            if (result.Succeeded)
            {
                await next().ConfigureAwait(false);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "The caller is not permitted to access this route.",
                    VeyraErrorCodes.Forbidden).ConfigureAwait(false);
                return;
            }

            await context.ChallengeAsync().ConfigureAwait(false);
        });
    }

    private static bool IsTruthy(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) &&
        bool.TryParse(value, out var parsed) &&
        parsed;

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string errorCode)
    {
        var problem = new ProblemDetails
        {
            Type = status == StatusCodes.Status401Unauthorized
                ? "https://tools.ietf.org/html/rfc7235#section-3.1"
                : "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["errorCode"] = errorCode,
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
            },
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json")
            .ConfigureAwait(false);
    }
}
