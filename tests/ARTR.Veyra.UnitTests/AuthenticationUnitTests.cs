using System.Security.Claims;
using System.Text.Encodings.Web;
using ARTR.Veyra.Authentication.ApiKey;
using ARTR.Veyra.Authentication.Authorization;
using ARTR.Veyra.Authentication.DependencyInjection;
using ARTR.Veyra.Authentication.Jwt;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Secrets;
using ARTR.Veyra.Core.Security;
using ARTR.Veyra.Infrastructure.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using VeyraAuthenticationOptions = ARTR.Veyra.Core.Configuration.AuthenticationOptions;

namespace ARTR.Veyra.UnitTests;

public sealed class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task HandleAuthenticateAsyncReturnsNoResultWhenHeaderMissing()
    {
        var handler = CreateHandler(new VeyraOptions());
        var context = new DefaultHttpContext();
        await handler.InitializeAsync(CreateScheme(), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncFailsWhenHeaderEmpty()
    {
        var handler = CreateHandler(CreateApiKeyOptions());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "";
        await handler.InitializeAsync(CreateScheme(), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("API key header was empty.", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncFailsWhenApiKeyNotRecognized()
    {
        var handler = CreateHandler(CreateApiKeyOptions());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "wrong-key";
        await handler.InitializeAsync(CreateScheme(), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("API key was not recognized.", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncSucceedsWithValidApiKeyAndRoles()
    {
        var handler = CreateHandler(CreateApiKeyOptions());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "demo-secret";
        await handler.InitializeAsync(CreateScheme(), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("demo-key", result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("admin", result.Principal?.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task HandleAuthenticateAsyncUsesOptionsHeaderNameOverride()
    {
        var veyraOptions = CreateApiKeyOptions();
        var handler = CreateHandler(veyraOptions, headerName: "X-Custom-Key");
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Custom-Key"] = "demo-secret";
        await handler.InitializeAsync(CreateScheme(), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    private static VeyraOptions CreateApiKeyOptions()
    {
        var hash = ApiKeyHasher.HashSha256Hex("demo-secret");
        return new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    HeaderName = "X-Api-Key",
                    Keys =
                    [
                        new ApiKeyEntry
                        {
                            Id = "demo-key",
                            Name = "demo",
                            HashSha256Hex = hash,
                            Roles = ["admin", ""],
                        },
                    ],
                },
            },
        };
    }

    private static ApiKeyAuthenticationHandler CreateHandler(VeyraOptions veyraOptions, string? headerName = null)
    {
        var apiKeyOptions = new ApiKeyAuthenticationOptions();
        if (!string.IsNullOrWhiteSpace(headerName))
        {
            apiKeyOptions.HeaderName = headerName;
        }

        return new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<ApiKeyAuthenticationOptions>(apiKeyOptions),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new StaticOptionsMonitor<VeyraOptions>(veyraOptions));
    }

    private static AuthenticationScheme CreateScheme() =>
        new(ApiKeyAuthenticationOptions.SchemeName, ApiKeyAuthenticationOptions.SchemeName, typeof(ApiKeyAuthenticationHandler));
}

public sealed class VeyraApiKeyAuthenticationPostConfigureOptionsTests
{
    [Fact]
    public void PostConfigureIgnoresNonApiKeyScheme()
    {
        var options = new ApiKeyAuthenticationOptions { HeaderName = "original" };
        var postConfigure = new VeyraApiKeyAuthenticationPostConfigureOptions(
            new StaticOptionsMonitor<VeyraOptions>(new VeyraOptions
            {
                Authentication = new VeyraAuthenticationOptions
                {
                    ApiKey = new ApiKeyOptions { HeaderName = "configured-header" },
                },
            }));

        postConfigure.PostConfigure("OtherScheme", options);

        Assert.Equal("original", options.HeaderName);
    }

    [Fact]
    public void PostConfigureSetsHeaderNameForApiKeyScheme()
    {
        var options = new ApiKeyAuthenticationOptions();
        var postConfigure = new VeyraApiKeyAuthenticationPostConfigureOptions(
            new StaticOptionsMonitor<VeyraOptions>(new VeyraOptions
            {
                Authentication = new VeyraAuthenticationOptions
                {
                    ApiKey = new ApiKeyOptions { HeaderName = "configured-header" },
                },
            }));

        postConfigure.PostConfigure(ApiKeyAuthenticationOptions.SchemeName, options);

        Assert.Equal("configured-header", options.HeaderName);
    }
}

public sealed class VeyraJwtBearerPostConfigureOptionsTests
{
    private const string SigningKeyVariable = "VEYRA_TEST_JWT_SIGNING_KEY";

    [Fact]
    public void PostConfigureIgnoresNonJwtScheme()
    {
        var options = new JwtBearerOptions();
        var postConfigure = CreatePostConfigure(new VeyraOptions());

        postConfigure.PostConfigure("OtherScheme", options);

        Assert.Null(options.Authority);
    }

    [Fact]
    public void PostConfigureReturnsWhenJwtDisabled()
    {
        var options = new JwtBearerOptions();
        var postConfigure = CreatePostConfigure(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions { Enabled = false },
            },
        });

        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        Assert.Null(options.Authority);
    }

    [Fact]
    public void PostConfigureReturnsWhenAuthenticationDisabled()
    {
        var options = new JwtBearerOptions();
        var postConfigure = CreatePostConfigure(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions { Enabled = false },
        });

        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        Assert.Null(options.Authority);
    }

    [Fact]
    public void PostConfigureSetsAuthorityAndMetadataAddress()
    {
        var options = new JwtBearerOptions();
        var postConfigure = CreatePostConfigure(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions
                {
                    Enabled = true,
                    Authority = "https://issuer.example.com",
                    MetadataAddress = "https://issuer.example.com/.well-known/openid-configuration",
                    Audience = "veyra",
                    Issuer = "issuer",
                    RequireHttpsMetadata = false,
                },
            },
        });

        postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

        Assert.Equal("https://issuer.example.com", options.Authority);
        Assert.Equal("https://issuer.example.com/.well-known/openid-configuration", options.MetadataAddress);
        Assert.Equal("veyra", options.Audience);
        Assert.False(options.RequireHttpsMetadata);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
    }

    [Fact]
    public void PostConfigureResolvesSigningKeyFromEnvironmentSecret()
    {
        const string signingKey = "01234567890123456789012345678901";
        Environment.SetEnvironmentVariable(SigningKeyVariable, signingKey);

        try
        {
            var options = new JwtBearerOptions();
            var postConfigure = CreatePostConfigure(new VeyraOptions
            {
                Authentication = new VeyraAuthenticationOptions
                {
                    Enabled = true,
                    Jwt = new JwtOptions
                    {
                        Enabled = true,
                        SigningKeySecretName = $"env:{SigningKeyVariable}",
                    },
                },
            });

            postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options);

            Assert.NotNull(options.TokenValidationParameters.IssuerSigningKey);
            Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
            Assert.Null(options.Authority);
            Assert.True(string.IsNullOrEmpty(options.MetadataAddress));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SigningKeyVariable, null);
        }
    }

    [Fact]
    public void PostConfigureThrowsWhenSigningKeySecretMissing()
    {
        var options = new JwtBearerOptions();
        var postConfigure = CreatePostConfigure(new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions
                {
                    Enabled = true,
                    SigningKeySecretName = "env:VEYRA_TEST_MISSING_JWT_SIGNING_KEY",
                },
            },
        });

        Assert.Throws<SecretResolutionException>(
            () => postConfigure.PostConfigure(JwtBearerDefaults.AuthenticationScheme, options));
    }

    private static VeyraJwtBearerPostConfigureOptions CreatePostConfigure(VeyraOptions veyraOptions) =>
        new(
            new StaticOptionsMonitor<VeyraOptions>(veyraOptions),
            new EnvironmentSecretResolver());
}

public sealed class AuthenticationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVeyraAuthenticationThrowsForNullServices()
    {
        Assert.Throws<ArgumentNullException>(
            () => ARTR.Veyra.Authentication.DependencyInjection.AuthenticationServiceCollectionExtensions.AddVeyraAuthentication(null!));
    }

    [Fact]
    public async Task AddVeyraAuthenticationRegistersPolicyScheme()
    {
        var veyraOptions = new VeyraOptions
        {
            Authentication = new VeyraAuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions { Enabled = true },
            },
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<VeyraOptions>>(Options.Create(veyraOptions));
        services.AddSingleton<IOptionsMonitor<VeyraOptions>>(new StaticOptionsMonitor<VeyraOptions>(veyraOptions));
        ARTR.Veyra.Authentication.DependencyInjection.AuthenticationServiceCollectionExtensions.AddVeyraAuthentication(services);

        await using var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(
            ARTR.Veyra.Authentication.DependencyInjection.AuthenticationServiceCollectionExtensions.PolicySchemeName);

        Assert.NotNull(scheme);
    }
}
