using System.Reflection;
using ARTR.Veyra.Authentication.ApiKey;
using ARTR.Veyra.Authentication.DependencyInjection;
using ARTR.Veyra.Authentication.Jwt;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Secrets;
using ARTR.Veyra.Infrastructure.Configuration;
using ARTR.Veyra.Infrastructure.RateLimiting;
using ARTR.Veyra.Infrastructure.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using VeyraAuthenticationOptions = ARTR.Veyra.Core.Configuration.AuthenticationOptions;

namespace ARTR.Veyra.UnitTests;

public sealed class JwtBearerChallengeBranchTests
{
    [Fact]
    public async Task OnChallenge_WritesProblemDetailsWhenNotPreviouslyHandled()
    {
        var options = new JwtBearerOptions();
        var postConfigure = new VeyraJwtBearerPostConfigureOptions(
            new StaticOptionsMonitor<VeyraOptions>(CreateJwtOptions()),
            new EnvironmentSecretResolver());
        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            null,
            typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext,
            scheme,
            options,
            new AuthenticationProperties());

        await options.Events.OnChallenge(challengeContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("VEYRA_AUTH_INVALID", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnChallenge_SkipsBodyWhenPriorHandlerAlreadyHandled()
    {
        var options = new JwtBearerOptions
        {
            Events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status418ImATeapot;
                    return Task.CompletedTask;
                },
            },
        };

        var postConfigure = new VeyraJwtBearerPostConfigureOptions(
            new StaticOptionsMonitor<VeyraOptions>(CreateJwtOptions()),
            new EnvironmentSecretResolver());
        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            null,
            typeof(JwtBearerHandler));
        var challengeContext = new JwtBearerChallengeContext(
            httpContext,
            scheme,
            options,
            new AuthenticationProperties());

        await options.Events.OnChallenge(challengeContext);

        Assert.Equal(StatusCodes.Status418ImATeapot, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task OnAuthenticationFailed_InvokesPriorHandlerWhenPresent()
    {
        var priorCalled = false;
        var options = new JwtBearerOptions
        {
            Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = _ =>
                {
                    priorCalled = true;
                    return Task.CompletedTask;
                },
            },
        };

        var postConfigure = new VeyraJwtBearerPostConfigureOptions(
            new StaticOptionsMonitor<VeyraOptions>(CreateJwtOptions()),
            new EnvironmentSecretResolver());
        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            null,
            typeof(JwtBearerHandler));
        var failedContext = new AuthenticationFailedContext(
            httpContext,
            scheme,
            options)
        {
            Exception = new InvalidOperationException("test"),
        };

        await options.Events.OnAuthenticationFailed(failedContext);

        Assert.True(priorCalled);
        Assert.NotNull(failedContext.Result);
        Assert.False(failedContext.Result!.Succeeded);
    }

    [Theory]
    [InlineData("a.b.c", true)]
    [InlineData("a.b.c.d.e", true)]
    [InlineData("not-a-valid-jwt", false)]
    [InlineData("a.b", false)]
    [InlineData(".b.c", false)]
    public void IsWellFormedCompactJwt_MatchesExpected(string token, bool expected)
    {
        Assert.Equal(expected, VeyraJwtBearerPostConfigureOptions.IsWellFormedCompactJwt(token));
    }

    [Fact]
    public void PostConfigureSetsMetadataAddressWhenAuthorityConfigured()
    {
        var options = new JwtBearerOptions();
        var postConfigure = new VeyraJwtBearerPostConfigureOptions(
            new StaticOptionsMonitor<VeyraOptions>(new VeyraOptions
            {
                Authentication = new VeyraAuthenticationOptions
                {
                    Enabled = true,
                    Jwt = new JwtOptions
                    {
                        Enabled = true,
                        Authority = "https://issuer.example.com",
                        MetadataAddress = "https://issuer.example.com/.well-known/openid-configuration",
                    },
                },
            }),
            new EnvironmentSecretResolver());

        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        Assert.Equal("https://issuer.example.com", options.Authority);
        Assert.Equal(
            "https://issuer.example.com/.well-known/openid-configuration",
            options.MetadataAddress);
    }

    private static VeyraOptions CreateJwtOptions() => new()
    {
        Authentication = new VeyraAuthenticationOptions
        {
            Enabled = true,
            Jwt = new JwtOptions
            {
                Enabled = true,
                Authority = "https://issuer.example.com",
                RequireHttpsMetadata = false,
            },
        },
    };
}

public sealed class ConfigurationActivationReloadBranchTests
{
    [Fact]
    public async Task Reload_ActivatesValidCandidateAndIncrementsGeneration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var monitor = new MutableOptionsMonitor<VeyraOptions>(CreateValidOptions());
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        monitor.Set(new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = true, RetainLastKnownGood = true },
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                GlobalPermitLimit = 50,
                GlobalWindowSeconds = 30,
            },
        });
        ((IConfigurationRoot)configuration).Reload();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(2, service.Generation);
        Assert.False(service.IsLastKnownGoodActive);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Fact]
    public async Task Reload_RetainsLastKnownGoodWhenValidatorThrows()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var monitor = new MutableOptionsMonitor<VeyraOptions>(CreateValidOptions());
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new ThrowingOnSecondCallValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        var generation = service.Generation;
        var fingerprint = service.Fingerprint;

        monitor.Set(CreateValidOptions());
        ((IConfigurationRoot)configuration).Reload();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(generation, service.Generation);
        Assert.Equal(fingerprint, service.Fingerprint);
        Assert.True(service.IsLastKnownGoodActive);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    private static VeyraOptions CreateValidOptions() => new()
    {
        Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
        ConfigurationReload = new ConfigurationReloadOptions { Enabled = true, RetainLastKnownGood = true },
    };

    private sealed class ThrowingOnSecondCallValidator : IValidateOptions<VeyraOptions>
    {
        private int _calls;

        public ValidateOptionsResult Validate(string? name, VeyraOptions options)
        {
            if (Interlocked.Increment(ref _calls) > 1)
            {
                throw new InvalidOperationException("validation exploded");
            }

            return ValidateOptionsResult.Success;
        }
    }
}

public sealed class PolicySchemeSelectionBranchTests
{
    [Fact]
    public void DualAuthSelectsApiKeyWhenHeaderPresent()
    {
        var context = CreateContext(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions { Enabled = true, Authority = "https://issuer.example.com" },
                ApiKey = new ApiKeyOptions { Enabled = true, HeaderName = "X-Api-Key" },
            },
        });
        context.Request.Headers["X-Api-Key"] = "demo-secret";

        Assert.Equal(ApiKeyAuthenticationOptions.SchemeName, InvokeSelectAuthenticationScheme(context));
    }

    [Fact]
    public void DualAuthDefaultsToJwtWhenHeadersMissingOrBlank()
    {
        var context = CreateContext(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions { Enabled = true, Authority = "https://issuer.example.com" },
                ApiKey = new ApiKeyOptions { Enabled = true, HeaderName = "X-Api-Key" },
            },
        });
        context.Request.Headers["X-Api-Key"] = "   ";

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, InvokeSelectAuthenticationScheme(context));
    }

    [Fact]
    public void JwtOnlySelectsJwtScheme()
    {
        var context = CreateContext(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions { Enabled = true, Authority = "https://issuer.example.com" },
            },
        });

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, InvokeSelectAuthenticationScheme(context));
    }

    [Fact]
    public void ApiKeyOnlySelectsApiKeyScheme()
    {
        var context = CreateContext(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions { Enabled = true },
            },
        });

        Assert.Equal(ApiKeyAuthenticationOptions.SchemeName, InvokeSelectAuthenticationScheme(context));
    }

    private static DefaultHttpContext CreateContext(VeyraOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static string InvokeSelectAuthenticationScheme(HttpContext context)
    {
        var method = typeof(ARTR.Veyra.Authentication.DependencyInjection.AuthenticationServiceCollectionExtensions).GetMethod(
            "SelectAuthenticationScheme",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method.Invoke(null, [context])!;
    }
}

public sealed class ApiKeyHandlerBranchTests
{
    [Fact]
    public async Task HandleAuthenticateAsyncUsesHandlerHeaderNameWhenConfigured()
    {
        var hash = ARTR.Veyra.Core.Security.ApiKeyHasher.HashSha256Hex("demo-secret");
        var veyraOptions = new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    HeaderName = "X-Api-Key",
                    Keys = [new ApiKeyEntry { Id = "k1", Name = "demo", HashSha256Hex = hash }],
                },
            },
        };

        var handlerOptions = new ApiKeyAuthenticationOptions { HeaderName = "X-Override-Key" };
        var handler = new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<ApiKeyAuthenticationOptions>(handlerOptions),
            NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            new StaticOptionsMonitor<VeyraOptions>(veyraOptions));

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Override-Key"] = "demo-secret";
        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyAuthenticationOptions.SchemeName, null, typeof(ApiKeyAuthenticationHandler)),
            context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
    }
}

public sealed class ConfigurationActivationConstructorTests
{
    [Fact]
    public void ConstructorThrowsForNullDependencies()
    {
        var configuration = new ConfigurationBuilder().Build();
        var monitor = new StaticOptionsMonitor<VeyraOptions>(new VeyraOptions());
        var validator = new VeyraOptionsValidator();
        var logger = NullLogger<ConfigurationActivationService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new ConfigurationActivationService(null!, monitor, validator, logger));
        Assert.Throws<ArgumentNullException>(() => new ConfigurationActivationService(configuration, null!, validator, logger));
        Assert.Throws<ArgumentNullException>(() => new ConfigurationActivationService(configuration, monitor, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new ConfigurationActivationService(configuration, monitor, validator, null!));
    }

    [Fact]
    public async Task Reload_DoesNotRetainLastKnownGoodWhenDisabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var monitor = new MutableOptionsMonitor<VeyraOptions>(new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = true, RetainLastKnownGood = false },
        });
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        monitor.Set(new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/" },
            ConfigurationReload = new ConfigurationReloadOptions { RetainLastKnownGood = false },
        });
        ((IConfigurationRoot)configuration).Reload();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.False(service.IsLastKnownGoodActive);
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }
}

public sealed class MemoryRateLimiterStoreBranchTests
{
    [Fact]
    public async Task TryAcquireAsync_ExpiresWindowInUpdateFactoryPath()
    {
        var store = new MemoryRateLimiterStore();
        const string key = "rolling-window";
        var cancellationToken = TestContext.Current.CancellationToken;
        var window = TimeSpan.FromMilliseconds(40);

        Assert.True(await store.TryAcquireAsync(key, permitLimit: 1, window, cancellationToken));
        Assert.False(await store.TryAcquireAsync(key, permitLimit: 1, window, cancellationToken));

        await Task.Delay(50, cancellationToken);

        Assert.True(await store.TryAcquireAsync(key, permitLimit: 1, window, cancellationToken));
        Assert.False(await store.TryAcquireAsync(key, permitLimit: 1, window, cancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_AllowsParallelKeysAfterIndependentWindows()
    {
        var store = new MemoryRateLimiterStore();
        var cancellationToken = TestContext.Current.CancellationToken;
        var window = TimeSpan.FromMilliseconds(30);

        Assert.True(await store.TryAcquireAsync("a", permitLimit: 1, window, cancellationToken));
        Assert.True(await store.TryAcquireAsync("b", permitLimit: 1, window, cancellationToken));

        await Task.Delay(40, cancellationToken);

        Assert.True(await store.TryAcquireAsync("a", permitLimit: 1, window, cancellationToken));
        Assert.True(await store.TryAcquireAsync("b", permitLimit: 1, window, cancellationToken));
    }
}
