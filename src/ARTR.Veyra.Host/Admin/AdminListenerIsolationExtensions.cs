using ARTR.Veyra.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ARTR.Veyra.Host.Admin;

public static class AdminListenerIsolationExtensions
{
    public static IApplicationBuilder UseVeyraAdminListenerIsolation(
        this IApplicationBuilder app,
        VeyraOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        var adminPorts = AdminOptions.ParseListenPorts(options.Admin.ListenUrls);
        if (adminPorts.Count == 0)
        {
            return app;
        }

        var adminPathPrefix = options.Admin.PathBase.TrimEnd('/');

        return app.Use(async (context, next) =>
        {
            var localPort = context.Connection.LocalPort;
            var isAdminListener = adminPorts.Contains(localPort);
            var isAdminPath = context.Request.Path.StartsWithSegments(adminPathPrefix, StringComparison.OrdinalIgnoreCase);

            if (isAdminListener && !isAdminPath)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!isAdminListener && isAdminPath)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next().ConfigureAwait(false);
        });
    }
}
