using System.Security.Claims;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Routing;
using ARTR.Veyra.Host.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace ARTR.Veyra.UnitTests;

public sealed class ReverseProxyAuthorizationMiddlewareTests
{
    [Fact]
    public async Task Skips_WhenAuthenticationDisabled()
    {
        var called = false;
        await InvokeAsync(
            new VeyraOptions { Authentication = new AuthenticationOptions { Enabled = false } },
            metadata: null,
            authenticated: false,
            next: () => { called = true; return Task.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task AllowsAnonymous_WhenMetadataSet()
    {
        var called = false;
        await InvokeAsync(
            EnabledOptions(),
            metadata: new Dictionary<string, string> { [VeyraRouteMetadata.AllowAnonymous] = "true" },
            authenticated: false,
            next: () => { called = true; return Task.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task Denies_WhenDenyByDefaultAndNoPolicy()
    {
        var context = await InvokeAsync(
            EnabledOptions(denyByDefault: true),
            metadata: new Dictionary<string, string>(),
            authenticated: false,
            next: () => Task.CompletedTask);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Continues_WhenDenyByDefaultOffAndNoPolicy()
    {
        var called = false;
        await InvokeAsync(
            EnabledOptions(denyByDefault: false),
            metadata: new Dictionary<string, string>(),
            authenticated: false,
            next: () => { called = true; return Task.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task Challenges_WhenPolicyFailsAndAnonymous()
    {
        var context = await InvokeAsync(
            EnabledOptions(),
            metadata: new Dictionary<string, string> { [VeyraRouteMetadata.AuthorizationPolicy] = "VeyraAdmin" },
            authenticated: false,
            next: () => Task.CompletedTask,
            registerFailingPolicy: true);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Forbids_WhenPolicyFailsAndAuthenticated()
    {
        var context = await InvokeAsync(
            EnabledOptions(),
            metadata: new Dictionary<string, string> { [VeyraRouteMetadata.AuthorizationPolicy] = "VeyraAdmin" },
            authenticated: true,
            next: () => Task.CompletedTask,
            registerFailingPolicy: true);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Continues_WhenPolicySucceeds()
    {
        var called = false;
        await InvokeAsync(
            EnabledOptions(),
            metadata: new Dictionary<string, string> { [VeyraRouteMetadata.AuthorizationPolicy] = "VeyraAdmin" },
            authenticated: true,
            next: () => { called = true; return Task.CompletedTask; },
            registerSucceedingPolicy: true);
        Assert.True(called);
    }

    private static VeyraOptions EnabledOptions(bool denyByDefault = true) => new()
    {
        Authentication = new AuthenticationOptions { Enabled = true },
        RoutingSecurity = new RoutingSecurityOptions { DenyAnonymousRoutesByDefault = denyByDefault },
    };

    private static async Task<HttpContext> InvokeAsync(
        VeyraOptions options,
        IReadOnlyDictionary<string, string>? metadata,
        bool authenticated,
        Func<Task> next,
        bool registerFailingPolicy = false,
        bool registerSucceedingPolicy = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptionsMonitor<VeyraOptions>>(new StaticMonitor(options));
        services.AddAuthentication("Test")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                "Test",
                _ => { });
        services.AddAuthorization(o =>
        {
            if (registerSucceedingPolicy)
            {
                o.AddPolicy("VeyraAdmin", p => p.RequireAssertion(_ => true));
            }
            else if (registerFailingPolicy)
            {
                o.AddPolicy("VeyraAdmin", p => p.RequireAssertion(_ => false));
            }
        });

        var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseVeyraReverseProxyAuthorization();
        app.Run(_ => next());
        var pipeline = app.Build();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "user")],
                authenticationType: "Test"));
        }

        if (metadata is not null)
        {
            var routeConfig = new RouteConfig
            {
                RouteId = "r1",
                ClusterId = "c1",
                Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
            };
            var routeModel = new RouteModel(routeConfig, cluster: null, transformer: HttpTransformer.Default);
            context.Features.Set<IReverseProxyFeature>(new TestProxyFeature(routeModel));
        }

        await pipeline(context);
        return context;
    }

    private sealed class TestProxyFeature(RouteModel route) : IReverseProxyFeature
    {
        public RouteModel Route { get; } = route;

        public ClusterModel Cluster { get; } = null!;

        public IReadOnlyList<DestinationState> AllDestinations => [];

        public IReadOnlyList<DestinationState> AvailableDestinations
        {
            get => [];
            set { }
        }

        public DestinationState? ProxiedDestination
        {
            get => null;
            set { }
        }
    }

    private sealed class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());

        protected override Task HandleChallengeAsync(Microsoft.AspNetCore.Authentication.AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    }

    private sealed class StaticMonitor(VeyraOptions value) : IOptionsMonitor<VeyraOptions>
    {
        public VeyraOptions CurrentValue => value;

        public VeyraOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<VeyraOptions, string?> listener) => null;
    }
}
