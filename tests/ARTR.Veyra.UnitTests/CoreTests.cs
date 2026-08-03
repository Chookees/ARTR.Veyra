using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Errors;
using ARTR.Veyra.Core.Security;
using ARTR.Veyra.Core.Transforms;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class VeyraErrorCodesTests
{
    [Fact]
    public void AllErrorCodesAreNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.AuthRequired));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.AuthInvalid));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.Forbidden));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.RateLimited));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.Validation));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.Upstream));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.NotFound));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.Internal));
        Assert.False(string.IsNullOrWhiteSpace(VeyraErrorCodes.ConfigInvalid));
    }

    [Fact]
    public void ErrorCodesUseExpectedPrefix()
    {
        string[] codes =
        [
            VeyraErrorCodes.AuthRequired,
            VeyraErrorCodes.AuthInvalid,
            VeyraErrorCodes.Forbidden,
            VeyraErrorCodes.RateLimited,
            VeyraErrorCodes.Validation,
            VeyraErrorCodes.Upstream,
            VeyraErrorCodes.NotFound,
            VeyraErrorCodes.Internal,
            VeyraErrorCodes.ConfigInvalid,
        ];

        Assert.All(codes, code => Assert.StartsWith("VEYRA_", code, StringComparison.Ordinal));
    }
}

public sealed class ApiKeyHasherTests
{
    [Fact]
    public void HashSha256HexProducesLowercaseHexDigest()
    {
        var hash = ApiKeyHasher.HashSha256Hex("test-key");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[a-f0-9]{64}$", hash);
    }

    [Fact]
    public void HashSha256HexIsDeterministic()
    {
        var first = ApiKeyHasher.HashSha256Hex("same-key");
        var second = ApiKeyHasher.HashSha256Hex("same-key");

        Assert.Equal(first, second);
    }

    [Fact]
    public void FixedTimeEqualsHexReturnsTrueForMatchingHashes()
    {
        var hash = ApiKeyHasher.HashSha256Hex("matching-key");

        Assert.True(ApiKeyHasher.FixedTimeEqualsHex(hash, hash));
    }

    [Fact]
    public void FixedTimeEqualsHexReturnsFalseForDifferentHashes()
    {
        var left = ApiKeyHasher.HashSha256Hex("left-key");
        var right = ApiKeyHasher.HashSha256Hex("right-key");

        Assert.False(ApiKeyHasher.FixedTimeEqualsHex(left, right));
    }

    [Fact]
    public void FixedTimeEqualsHexReturnsFalseForInvalidHex()
    {
        var hash = ApiKeyHasher.HashSha256Hex("valid-key");

        Assert.False(ApiKeyHasher.FixedTimeEqualsHex(hash, "not-a-valid-hash"));
    }
}

public sealed class TransformAllowlistTests
{
    [Fact]
    public void DefaultAllowlistContainsCommonYarpTransforms()
    {
        Assert.Contains("PathPrefix", TransformAllowlist.DefaultAllowlist);
        Assert.Contains("RequestHeader", TransformAllowlist.DefaultAllowlist);
        Assert.Contains("X-Forwarded", TransformAllowlist.DefaultAllowlist);
    }

    [Fact]
    public void ValidateAcceptsKnownTransform()
    {
        IReadOnlyDictionary<string, object?>[] transforms =
        [
            new Dictionary<string, object?> { ["PathPrefix"] = "/api" },
        ];

        var result = TransformAllowlist.Validate(transforms);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateAcceptsTransformWithParameterKeys()
    {
        IReadOnlyDictionary<string, object?>[] transforms =
        [
            new Dictionary<string, object?>
            {
                ["RequestHeader"] = "X-Forwarded-Host",
                ["Set"] = "example.com",
            },
        ];

        var result = TransformAllowlist.Validate(transforms);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsUnknownTransformKey()
    {
        IReadOnlyDictionary<string, object?>[] transforms =
        [
            new Dictionary<string, object?> { ["EvilTransform"] = "value" },
        ];

        var result = TransformAllowlist.Validate(transforms);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("EvilTransform", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsMultipleTransformKeysInOneEntry()
    {
        IReadOnlyDictionary<string, object?>[] transforms =
        [
            new Dictionary<string, object?>
            {
                ["PathPrefix"] = "/api",
                ["PathSet"] = "/fixed",
            },
        ];

        var result = TransformAllowlist.Validate(transforms);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("multiple transform keys", StringComparison.Ordinal));
    }
}

public sealed class VeyraOptionsValidatorTests
{
    private readonly VeyraOptionsValidator _validator = new();

    [Fact]
    public void ValidateSucceedsForMinimalValidOptions()
    {
        var result = _validator.Validate(null, new VeyraOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsWhenAuthenticationEnabledWithoutSchemes()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions { Enabled = true },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("Jwt.Enabled or ApiKey.Enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenApiKeyEnabledWithoutKeys()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                ApiKey = new ApiKeyOptions { Enabled = true },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("ApiKey.Keys", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsForInvalidApiKeyHash()
    {
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
                            Id = "key-1",
                            Name = "Primary",
                            HashSha256Hex = "invalid",
                        },
                    ],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("HashSha256Hex", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsWhenJwtEnabledWithoutAuthorityOrSecret()
    {
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions
            {
                Enabled = true,
                Jwt = new JwtOptions { Enabled = true },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("SigningKeySecretName", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsForDuplicateRateLimitPolicyNames()
    {
        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies =
                [
                    new RateLimitPolicyOptions { Name = "default", PermitLimit = 10, WindowSeconds = 60 },
                    new RateLimitPolicyOptions { Name = "default", PermitLimit = 20, WindowSeconds = 60 },
                ],
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("duplicated", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateFailsForInvalidAdminPathBase()
    {
        var options = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "relative-path" },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("Admin.PathBase", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAcceptsValidAuthenticationConfiguration()
    {
        var hash = ApiKeyHasher.HashSha256Hex("secret-api-key");
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
                            Id = "key-1",
                            Name = "Primary",
                            HashSha256Hex = hash,
                            Roles = ["admin"],
                        },
                    ],
                },
            },
            Authorization = new AuthorizationOptions
            {
                Enabled = true,
                Policies = new Dictionary<string, string[]>
                {
                    ["admin-only"] = ["admin"],
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
