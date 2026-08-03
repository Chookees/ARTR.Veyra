using System.Threading.RateLimiting;
using ARTR.Veyra.Authentication.Authorization;
using ARTR.Veyra.Authentication.DependencyInjection;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.DependencyInjection;
using ARTR.Veyra.Core.Hosting;
using ARTR.Veyra.Host.Admin;
using ARTR.Veyra.Host.Health;
using ARTR.Veyra.Host.Middleware;
using ARTR.Veyra.Host.RateLimiting;
using ARTR.Veyra.Infrastructure.DependencyInjection;
using ARTR.Veyra.Infrastructure.Transforms;
using ARTR.Veyra.Observability.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("config/veyra.example.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"config/veyra.{builder.Environment.EnvironmentName.ToLowerInvariant()}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: VeyraConstants.EnvPrefix)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Host.UseWindowsService(options => options.ServiceName = VeyraConstants.ProductName);
builder.Host.UseSystemd();

builder.Services.AddVeyraCore(builder.Configuration);
builder.Services.AddVeyraInfrastructure(builder.Configuration);
builder.Services.AddVeyraAuthentication();
builder.Services.AddVeyraObservability(builder.Configuration);
builder.Services.AddVeyraRateLimiting(builder.Configuration);
builder.Services.AddVeyraHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.Configure<HostOptions>(options =>
{
    var shutdownSeconds = builder.Configuration.GetSection(VeyraOptions.SectionName)
        .Get<VeyraOptions>()?.Shutdown.ShutdownTimeoutSeconds ?? 30;
    options.ShutdownTimeout = TimeSpan.FromSeconds(shutdownSeconds);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var veyra = context.Configuration.GetSection(VeyraOptions.SectionName).Get<VeyraOptions>()
        ?? new VeyraOptions();
    var limits = veyra.RequestLimits;
    options.Limits.MaxRequestBodySize = limits.MaxRequestBodyBytes;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(limits.RequestHeadersTimeoutSeconds);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(limits.KeepAliveTimeoutSeconds);

    // When any Listen() is used, UseUrls is ignored — bind data-plane and optional admin listeners explicitly.
    if (!string.IsNullOrWhiteSpace(veyra.Admin.ListenUrls))
    {
        var dataUrls = context.Configuration["Urls"] ?? "http://127.0.0.1:5080";
        foreach (var part in dataUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Concat(veyra.Admin.ListenUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (!Uri.TryCreate(part, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var host = uri.Host is "localhost" or "+" or "*" ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Parse(uri.Host);
            options.Listen(host, uri.Port);
        }
    }
});

var app = builder.Build();

var veyraOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<VeyraOptions>>().Value;

var transformValidation = app.Services.GetRequiredService<YarpTransformAllowlistValidator>()
    .ValidateConfiguredTransforms();
if (!transformValidation.IsValid)
{
    throw new InvalidOperationException(
        "ReverseProxy transforms failed allowlist validation: " + string.Join("; ", transformValidation.Errors));
}

app.UseVeyraExceptionHandler();
app.UseVeyraCorrelation();
app.UseVeyraAdminListenerIsolation(veyraOptions);

if (veyraOptions.ForwardedHeaders.Enabled)
{
    var forwarded = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
        ForwardLimit = veyraOptions.ForwardedHeaders.ForwardLimit,
    };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    foreach (var proxy in veyraOptions.ForwardedHeaders.KnownProxies)
    {
        if (System.Net.IPAddress.TryParse(proxy, out var ip))
        {
            forwarded.KnownProxies.Add(ip);
        }
    }

    foreach (var network in veyraOptions.ForwardedHeaders.KnownNetworks)
    {
        if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
        {
            forwarded.KnownIPNetworks.Add(ipNetwork);
        }
    }

    app.UseForwardedHeaders(forwarded);
}

if (veyraOptions.Tls.UseHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseVeyraAuthenticationProblemDetails();

if (veyraOptions.RateLimiting.Enabled)
{
    app.UseRateLimiter();
}

var adminPath = veyraOptions.Admin.PathBase.TrimEnd('/');
if (veyraOptions.Admin.Enabled)
{
    app.MapVeyraAdmin(adminPath, veyraOptions);
}

if (veyraOptions.Health.Enabled)
{
    app.MapVeyraHealth(adminPath, veyraOptions);
}

app.MapVeyraPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(adminPath + "/openapi/{documentName}.json");
    app.MapScalarApiReference(adminPath + "/docs", options =>
    {
        options.WithTitle(VeyraConstants.ProductName);
    });
}

app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
