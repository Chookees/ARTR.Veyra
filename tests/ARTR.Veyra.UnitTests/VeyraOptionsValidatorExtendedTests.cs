using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class VeyraOptionsValidatorExtendedTests
{
    private readonly VeyraOptionsValidator _validator = new();

    [Fact]
    public void ValidateThrowsWhenOptionsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.Validate(null, null!));
    }

    [Fact]
    public void ValidateFailsWhenAdminPathBaseIsRoot()
    {
        var options = new VeyraOptions { Admin = new AdminOptions { PathBase = "/" } };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("cannot be the root path", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenAdminListenUrlsContainsInvalidUrl()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions { ListenUrls = "http://127.0.0.1:5081;not-valid" },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Admin.ListenUrls contains an invalid URL", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAcceptsValidAdminListenUrls()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions { ListenUrls = "http://127.0.0.1:5081;https://127.0.0.1:5082" },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsWhenAdminListenUrlsUsesUnsupportedScheme()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions { ListenUrls = "ftp://127.0.0.1:5081" },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Admin.ListenUrls contains an invalid URL", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenJwtAuthorityIsInvalidUri()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions { Enabled = true, Authority = "not-a-uri" },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Authority must be an absolute URI", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenJwtMetadataAddressIsInvalidUri()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions
                {
                    Enabled = true,
                    Authority = "https://issuer.example.com",
                    MetadataAddress = "relative-metadata",
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("MetadataAddress must be an absolute URI", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAcceptsJwtWithAuthorityOnly()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions
                {
                    Enabled = true,
                    Authority = "https://issuer.example.com",
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateAcceptsJwtWithSigningKeySecretNameOnly()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions
                {
                    Enabled = true,
                    SigningKeySecretName = "env:VEYRA_JWT_KEY",
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsWhenApiKeyHeaderNameMissing()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    HeaderName = "  ",
                    Keys = [new ApiKeyEntry { Id = "k1", Name = "Primary", HashSha256Hex = hash }],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("HeaderName is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenApiKeyIdMissing()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    Keys = [new ApiKeyEntry { Id = "", Name = "Primary", HashSha256Hex = hash }],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains(".Id is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenApiKeyIdDuplicated()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    Keys =
                    [
                        new ApiKeyEntry { Id = "dup", Name = "First", HashSha256Hex = hash },
                        new ApiKeyEntry { Id = "dup", Name = "Second", HashSha256Hex = hash },
                    ],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("is duplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenApiKeyNameMissing()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    Keys = [new ApiKeyEntry { Id = "k1", Name = "", HashSha256Hex = hash }],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains(".Name is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenApiKeyHashMissing()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    Keys = [new ApiKeyEntry { Id = "k1", Name = "Primary", HashSha256Hex = "" }],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("HashSha256Hex is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenApiKeyRolesContainEmptyValue()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions
                {
                    Enabled = true,
                    Keys =
                    [
                        new ApiKeyEntry
                        {
                            Id = "k1",
                            Name = "Primary",
                            HashSha256Hex = hash,
                            Roles = ["admin", ""],
                        },
                    ],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Roles cannot contain empty values", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenAuthorizationEnabledWithoutPolicies()
    {
        var options = new VeyraOptions
        {
            Authorization = new AuthorizationOptions { Enabled = true },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("requires at least one policy", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenAuthorizationPolicyNameEmpty()
    {
        var options = new VeyraOptions
        {
            Authorization = new AuthorizationOptions
            {
                Enabled = true,
                Policies = new Dictionary<string, string[]> { [""] = ["admin"] },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("empty policy name", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenAuthorizationPolicyHasNoRoles()
    {
        var options = new VeyraOptions
        {
            Authorization = new AuthorizationOptions
            {
                Enabled = true,
                Policies = new Dictionary<string, string[]> { ["admin-only"] = [] },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("must contain at least one role", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenAuthorizationPolicyContainsEmptyRole()
    {
        var options = new VeyraOptions
        {
            Authorization = new AuthorizationOptions
            {
                Enabled = true,
                Policies = new Dictionary<string, string[]> { ["admin-only"] = ["admin", ""] },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("cannot contain empty role values", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRateLimitingGlobalPermitLimitInvalid()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                GlobalPermitLimit = 0,
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("GlobalPermitLimit", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRateLimitingGlobalWindowSecondsInvalid()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                GlobalWindowSeconds = 0,
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("GlobalWindowSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRateLimitPolicyNameMissing()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies = [new RateLimitPolicyOptions { Name = "", PermitLimit = 10, WindowSeconds = 60 }],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains(".Name is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRateLimitPolicyPermitLimitInvalid()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies = [new RateLimitPolicyOptions { Name = "p1", PermitLimit = 0, WindowSeconds = 60 }],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("PermitLimit must be greater than zero", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRateLimitPolicyWindowSecondsInvalid()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies = [new RateLimitPolicyOptions { Name = "p1", PermitLimit = 10, WindowSeconds = 0 }],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("WindowSeconds must be greater than zero", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRateLimitPolicyQueueLimitNegative()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies = [new RateLimitPolicyOptions { Name = "p1", PermitLimit = 10, WindowSeconds = 60, QueueLimit = -1 }],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("QueueLimit cannot be negative", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenRequestLimitsInvalid()
    {
        var options = new VeyraOptions
        {
            RequestLimits = new RequestLimitsOptions
            {
                MaxRequestBodyBytes = 0,
                RequestHeadersTimeoutSeconds = 0,
                KeepAliveTimeoutSeconds = 0,
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("MaxRequestBodyBytes", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("RequestHeadersTimeoutSeconds", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("KeepAliveTimeoutSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenForwardedHeadersForwardLimitNegative()
    {
        var options = new VeyraOptions
        {
            ForwardedHeaders = new ForwardedHeadersOptions
            {
                Enabled = true,
                ForwardLimit = -1,
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("ForwardLimit cannot be negative", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenForwardedHeadersKnownProxyInvalid()
    {
        var options = new VeyraOptions
        {
            ForwardedHeaders = new ForwardedHeadersOptions
            {
                Enabled = true,
                KnownProxies = ["not-an-ip"],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("invalid IP address", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenForwardedHeadersKnownNetworkInvalid()
    {
        var options = new VeyraOptions
        {
            ForwardedHeaders = new ForwardedHeadersOptions
            {
                Enabled = true,
                KnownNetworks = ["invalid-cidr"],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("invalid CIDR network", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAcceptsValidForwardedHeadersConfiguration()
    {
        var options = new VeyraOptions
        {
            ForwardedHeaders = new ForwardedHeadersOptions
            {
                Enabled = true,
                KnownProxies = ["127.0.0.1"],
                KnownNetworks = ["10.0.0.0/8", "2001:db8::/32"],
                ForwardLimit = 2,
            },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsWhenObservabilityServiceNameMissing()
    {
        var options = new VeyraOptions
        {
            Observability = new ObservabilityOptions { ServiceName = "" },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("ServiceName is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenOtlpEnabledWithoutEndpoint()
    {
        var options = new VeyraOptions
        {
            Observability = new ObservabilityOptions
            {
                Otlp = new OtlpExporterOptions { Enabled = true },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Otlp.Endpoint is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenOtlpEndpointInvalid()
    {
        var options = new VeyraOptions
        {
            Observability = new ObservabilityOptions
            {
                Otlp = new OtlpExporterOptions { Enabled = true, Endpoint = "not-a-uri" },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Otlp.Endpoint must be an absolute URI", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenPrometheusPathInvalid()
    {
        var options = new VeyraOptions
        {
            Observability = new ObservabilityOptions
            {
                Prometheus = new PrometheusExporterOptions { Enabled = true, Path = "metrics" },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Prometheus.Path must be an absolute path", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenSecretProviderTypeMissing()
    {
        var options = new VeyraOptions
        {
            Secrets = new SecretsOptions
            {
                Providers = [new SecretProviderOptions { Type = "" }],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains(".Type is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenSecretProviderNameDuplicated()
    {
        var options = new VeyraOptions
        {
            Secrets = new SecretsOptions
            {
                Providers =
                [
                    new SecretProviderOptions { Type = "Env", Name = "primary" },
                    new SecretProviderOptions { Type = "Env", Name = "primary" },
                ],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("is duplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenFileSecretProviderMissingPath()
    {
        var options = new VeyraOptions
        {
            Secrets = new SecretsOptions
            {
                Providers = [new SecretProviderOptions { Type = "File", Name = "secrets" }],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Path is required for File secret providers", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenHealthPathsInvalid()
    {
        var options = new VeyraOptions
        {
            Health = new HealthOptions
            {
                Enabled = true,
                LivePath = "live",
                ReadyPath = "ready",
                StartupPath = "startup",
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("LivePath", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("ReadyPath", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("StartupPath", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenTransformsAllowlistContainsEmptyValue()
    {
        var options = new VeyraOptions
        {
            Transforms = new TransformsOptions { Allowlist = ["PathPrefix", ""] },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("cannot contain empty values", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenTransformsAllowlistContainsDuplicates()
    {
        var options = new VeyraOptions
        {
            Transforms = new TransformsOptions { Allowlist = ["PathPrefix", "pathprefix"] },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("duplicate entries", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenTransformsAllowlistContainsUnknownKey()
    {
        var options = new VeyraOptions
        {
            Transforms = new TransformsOptions { Allowlist = ["EvilTransform"] },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("not present in the default allowlist", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenShutdownTimeoutInvalid()
    {
        var options = new VeyraOptions
        {
            Shutdown = new ShutdownOptions { ShutdownTimeoutSeconds = 0 },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("ShutdownTimeoutSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsForInvalidCidrPrefixLength()
    {
        var options = new VeyraOptions
        {
            ForwardedHeaders = new ForwardedHeadersOptions
            {
                Enabled = true,
                KnownNetworks = ["10.0.0.0/33"],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("invalid CIDR network", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsForMalformedCidrNetwork()
    {
        var options = new VeyraOptions
        {
            ForwardedHeaders = new ForwardedHeadersOptions
            {
                Enabled = true,
                KnownNetworks = ["10.0.0.0"],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("invalid CIDR network", StringComparison.Ordinal));
    }
}
