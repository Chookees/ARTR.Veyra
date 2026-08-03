using System.Diagnostics;
using ARTR.Veyra.Core.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARTR.Veyra.Host.Middleware;

public static class AuthenticationProblemDetailsExtensions
{
    public static IApplicationBuilder UseVeyraAuthenticationProblemDetails(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            await next().ConfigureAwait(false);

            if (context.Response.HasStarted)
            {
                return;
            }

            if (context.Response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
            {
                return;
            }

            // If a scheme already wrote a body (e.g. JWT OnChallenge), leave it alone.
            if (context.Response.ContentLength is > 0 ||
                !string.IsNullOrEmpty(context.Response.ContentType))
            {
                return;
            }

            var unauthorized = context.Response.StatusCode == StatusCodes.Status401Unauthorized;
            var problem = new ProblemDetails
            {
                Type = unauthorized
                    ? "https://tools.ietf.org/html/rfc7235#section-3.1"
                    : "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                Title = unauthorized ? "Unauthorized" : "Forbidden",
                Status = context.Response.StatusCode,
                Detail = unauthorized
                    ? "Authentication is required or the provided credentials are invalid."
                    : "The caller is not permitted to access this resource.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["errorCode"] = unauthorized ? VeyraErrorCodes.AuthInvalid : VeyraErrorCodes.Forbidden,
                    ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
                },
            };

            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json").ConfigureAwait(false);
        });
    }
}
