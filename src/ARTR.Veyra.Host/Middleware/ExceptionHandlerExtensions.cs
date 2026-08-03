using System.Diagnostics;
using ARTR.Veyra.Core.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ARTR.Veyra.Host.Middleware;

public static partial class ExceptionHandlerExtensions
{
    public static IApplicationBuilder UseVeyraExceptionHandler(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ARTR.Veyra.ExceptionHandler");
                var env = context.RequestServices.GetRequiredService<IHostEnvironment>();

                var exception = feature?.Error;
                if (IsAuthenticationTokenException(exception))
                {
                    LogAuthenticationException(logger, exception!, context.Request.Method, context.Request.Path);
                    var unauthorized = new ProblemDetails
                    {
                        Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                        Title = "Unauthorized",
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "Authentication is required or the provided credentials are invalid.",
                        Instance = context.Request.Path,
                        Extensions =
                        {
                            ["errorCode"] = VeyraErrorCodes.AuthInvalid,
                            ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
                        },
                    };

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(unauthorized).ConfigureAwait(false);
                    return;
                }

                if (exception is not null)
                {
                    LogUnhandledException(logger, exception, context.Request.Method, context.Request.Path);
                }

                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = env.IsDevelopment() && exception is not null
                        ? exception.Message
                        : "An unexpected error occurred.",
                    Instance = context.Request.Path,
                    Extensions =
                    {
                        ["errorCode"] = VeyraErrorCodes.Internal,
                        ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
                    },
                };

                context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
            });
        });

        return app;
    }

    private static bool IsAuthenticationTokenException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SecurityTokenException or AuthenticationFailureException)
            {
                return true;
            }

            var typeName = current.GetType().FullName ?? current.GetType().Name;
            if (typeName.Contains("SecurityToken", StringComparison.Ordinal) ||
                typeName.Contains("JsonWebToken", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, PathString path);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Authentication token failure while processing {Method} {Path}")]
    private static partial void LogAuthenticationException(ILogger logger, Exception exception, string method, PathString path);
}
