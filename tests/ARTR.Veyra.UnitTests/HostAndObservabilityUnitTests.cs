using System.Net;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Host.Middleware;
using ARTR.Veyra.Observability.DependencyInjection;
using ARTR.Veyra.Observability.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class ExceptionHandlerExtensionsTests
{
    [Fact]
    public async Task UseVeyraExceptionHandlerReturnsProblemDetailsWithoutExceptionDetailInProduction()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.UseVeyraExceptionHandler();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/throw")
            {
                throw new InvalidOperationException("unit test exception");
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await next(context);
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("VEYRA_INTERNAL", body, StringComparison.Ordinal);
        Assert.DoesNotContain("unit test exception", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseVeyraExceptionHandlerIncludesExceptionDetailInDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.UseVeyraExceptionHandler();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/throw")
            {
                throw new InvalidOperationException("development exception detail");
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await next(context);
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("development exception detail", body, StringComparison.Ordinal);
    }
}

public sealed class ObservabilityApplicationBuilderExtensionsTests
{
    [Fact]
    public async Task MapVeyraPrometheusScrapingEndpointSkipsMappingWhenDisabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Options.Create(new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra" },
            Observability = new ObservabilityOptions
            {
                Prometheus = new PrometheusExporterOptions { Enabled = false },
            },
        }));

        var app = builder.Build();
        app.MapVeyraPrometheusScrapingEndpoint();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/_veyra/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MapVeyraPrometheusScrapingEndpointMapsMetricsWhenEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Options.Create(new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra" },
            Observability = new ObservabilityOptions
            {
                Prometheus = new PrometheusExporterOptions { Enabled = true, Path = "/metrics" },
            },
        }));
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(VeyraInstrumentation.MeterName);
                metrics.AddPrometheusExporter();
            });

        var app = builder.Build();
        app.MapVeyraPrometheusScrapingEndpoint();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/_veyra/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapVeyraPrometheusScrapingEndpointNormalizesRelativeMetricsPath()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Options.Create(new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra" },
            Observability = new ObservabilityOptions
            {
                Prometheus = new PrometheusExporterOptions { Enabled = true, Path = "metrics" },
            },
        }));
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddPrometheusExporter());

        var app = builder.Build();
        app.MapVeyraPrometheusScrapingEndpoint();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/_veyra/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class AuthenticationProblemDetailsExtensionsTests
{
    [Fact]
    public async Task MiddlewareWritesUnauthorizedProblemDetailsWhenResponseEmpty()
    {
        using var host = await CreateHostAsync(StatusCodes.Status401Unauthorized, contentType: null);
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("VEYRA_AUTH_INVALID", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MiddlewareWritesForbiddenProblemDetailsWhenResponseEmpty()
    {
        using var host = await CreateHostAsync(StatusCodes.Status403Forbidden, contentType: null);
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("VEYRA_FORBIDDEN", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MiddlewareSkipsWhenContentTypeAlreadySet()
    {
        using var host = await CreateHostAsync(StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("existing-body", body);
    }

    [Fact]
    public async Task MiddlewareSkipsForNonAuthStatusCodes()
    {
        using var host = await CreateHostAsync(StatusCodes.Status404NotFound, contentType: null);
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, body);
    }

    private static async Task<WebApplication> CreateHostAsync(int statusCode, string? contentType)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseVeyraAuthenticationProblemDetails();
        app.Run(async context =>
        {
            context.Response.StatusCode = statusCode;
            if (!string.IsNullOrEmpty(contentType))
            {
                context.Response.ContentType = contentType;
                await context.Response.WriteAsync("existing-body", TestContext.Current.CancellationToken);
            }
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
