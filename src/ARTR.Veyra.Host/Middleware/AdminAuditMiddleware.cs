using System.Diagnostics;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ARTR.Veyra.Host.Middleware;

public static partial class AdminAuditMiddleware
{
    public static IApplicationBuilder UseVeyraAdminAudit(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<VeyraOptions>>()
                .CurrentValue;
            var pathBase = options.Admin.PathBase.TrimEnd('/');
            var path = context.Request.Path.Value ?? string.Empty;
            var isAdmin = path.StartsWith(pathBase, StringComparison.OrdinalIgnoreCase);

            await next().ConfigureAwait(false);

            if (!isAdmin)
            {
                return;
            }

            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ARTR.Veyra.AdminAudit");
            LogAdminAccess(
                logger,
                context.Request.Method,
                path,
                context.Response.StatusCode,
                context.User.Identity?.IsAuthenticated == true,
                Activity.Current?.Id ?? context.TraceIdentifier);
        });
    }

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Information,
        Message = "Admin access method={Method} path={Path} status={StatusCode} authenticated={Authenticated} traceId={TraceId}")]
    private static partial void LogAdminAccess(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        bool authenticated,
        string traceId);
}
