using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Host.Admin;
using ARTR.Veyra.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class AdminIsolationMiddlewareTests
{
    [Fact]
    public async Task DataPlaneRejectsAdminPathWhenAdminListenerConfigured()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions
            {
                PathBase = "/_veyra",
                ListenUrls = "http://127.0.0.1:5081",
            },
        };

        var context = new DefaultHttpContext();
        context.Connection.LocalPort = 5080;
        context.Request.Path = "/_veyra/info";

        await InvokeIsolationAsync(options, context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task AdminListenerRejectsNonAdminPath()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions
            {
                PathBase = "/_veyra",
                ListenUrls = "http://127.0.0.1:5081",
            },
        };

        var context = new DefaultHttpContext();
        context.Connection.LocalPort = 5081;
        context.Request.Path = "/a/hello";

        await InvokeIsolationAsync(options, context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task AdminListenerAllowsAdminPath()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions
            {
                PathBase = "/_veyra",
                ListenUrls = "http://127.0.0.1:5081",
            },
        };

        var context = new DefaultHttpContext();
        context.Connection.LocalPort = 5081;
        context.Request.Path = "/_veyra/info";
        var called = false;

        await InvokeIsolationAsync(options, context, () =>
        {
            called = true;
            return Task.CompletedTask;
        });

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode is 0 ? 200 : context.Response.StatusCode);
    }

    [Fact]
    public async Task NoIsolationWhenListenUrlsMissing()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra" },
        };

        var context = new DefaultHttpContext();
        context.Request.Path = "/_veyra/info";
        var called = false;
        await InvokeIsolationAsync(options, context, () =>
        {
            called = true;
            return Task.CompletedTask;
        });
        Assert.True(called);
    }

    private static async Task InvokeIsolationAsync(
        VeyraOptions options,
        HttpContext context,
        Func<Task>? next = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseVeyraAdminListenerIsolation(options);
        app.Run(async ctx =>
        {
            if (next is not null)
            {
                await next().ConfigureAwait(false);
            }

            if (ctx.Response.StatusCode == 0)
            {
                ctx.Response.StatusCode = StatusCodes.Status200OK;
            }
        });
        var pipeline = app.Build();
        await pipeline(context);
    }
}

public sealed class ConfigurationActivationFailureTests
{
    [Fact]
    public async Task ReloadRejectsInvalidCandidateAndKeepsLastKnownGood()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var valid = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = true, RetainLastKnownGood = true },
        };
        var monitor = new MutableOptionsMonitor(valid);
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        var generation = service.Generation;
        var fingerprint = service.Fingerprint;

        monitor.CurrentValue = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", ListenUrls = "bad" },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = true, RetainLastKnownGood = true },
        };
        monitor.RaiseChange();

        // Allow change callback to run
        await Task.Delay(50);

        Assert.Equal(generation, service.Generation);
        Assert.Equal(fingerprint, service.Fingerprint);
        Assert.True(service.IsLastKnownGoodActive);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Fact]
    public async Task ReloadSkippedWhenDisabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var valid = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = false, RetainLastKnownGood = true },
        };
        var monitor = new MutableOptionsMonitor(valid);
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        var generation = service.Generation;

        monitor.CurrentValue = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
            RateLimiting = new RateLimitingOptions { Enabled = true, GlobalPermitLimit = 1, GlobalWindowSeconds = 1 },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = false },
        };
        monitor.RaiseChange();
        await Task.Delay(50);

        Assert.Equal(generation, service.Generation);
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    private sealed class MutableOptionsMonitor : IOptionsMonitor<VeyraOptions>
    {
        private Action<VeyraOptions, string?>? _listener;

        public MutableOptionsMonitor(VeyraOptions current) => CurrentValue = current;

        public VeyraOptions CurrentValue { get; set; }

        public VeyraOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<VeyraOptions, string?> listener)
        {
            _listener = listener;
            return new CallbackDisposable(() => _listener = null);
        }

        public void RaiseChange() => _listener?.Invoke(CurrentValue, Options.DefaultName);

        private sealed class CallbackDisposable(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
