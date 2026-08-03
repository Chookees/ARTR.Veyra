using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ARTR.Veyra.FunctionalTests;

internal sealed class LoopbackUpstreamHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private LoopbackUpstreamHost(WebApplication app, string baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress.TrimEnd('/') + "/";
    }

    public string BaseAddress { get; }

    public static async Task<LoopbackUpstreamHost> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.MapGet("/hello", (HttpRequest request, HttpResponse response) =>
        {
            var correlationId = request.Headers["X-Correlation-ID"].ToString();
            response.Headers["X-Correlation-ID"] = correlationId;
            return Results.Json(new { message = "Hello from upstream", correlationId });
        });

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        var address = app.Urls.First();
        return new LoopbackUpstreamHost(app, address);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
