using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Secrets;
using ARTR.Veyra.Core.Security;
using ARTR.Veyra.Core.Transforms;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class SecretResolutionExceptionTests
{
    [Fact]
    public void DefaultConstructorSetsEmptySecretName()
    {
        var exception = new SecretResolutionException();

        Assert.Equal(string.Empty, exception.SecretName);
    }

    [Fact]
    public void MessageConstructorSetsEmptySecretName()
    {
        var exception = new SecretResolutionException("resolution failed");

        Assert.Equal("resolution failed", exception.Message);
        Assert.Equal(string.Empty, exception.SecretName);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsEmptySecretName()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new SecretResolutionException("resolution failed", inner);

        Assert.Same(inner, exception.InnerException);
        Assert.Equal(string.Empty, exception.SecretName);
    }

    [Fact]
    public void SecretNameConstructorSetsSecretName()
    {
        var exception = new SecretResolutionException("jwt-key", "missing secret");

        Assert.Equal("jwt-key", exception.SecretName);
        Assert.Equal("missing secret", exception.Message);
    }

    [Fact]
    public void SecretNameAndInnerExceptionConstructorSetsSecretName()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new SecretResolutionException("jwt-key", "missing secret", inner);

        Assert.Equal("jwt-key", exception.SecretName);
        Assert.Same(inner, exception.InnerException);
    }
}

public sealed class ApiKeyHasherExtendedTests
{
    [Fact]
    public void HashSha256HexThrowsForNullOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => ApiKeyHasher.HashSha256Hex(""));
        Assert.Throws<ArgumentException>(() => ApiKeyHasher.HashSha256Hex("   "));
    }

    [Fact]
    public void FixedTimeEqualsHexReturnsFalseForDifferentLengths()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");

        Assert.False(ApiKeyHasher.FixedTimeEqualsHex(hash, hash[..^1]));
    }

    [Fact]
    public void FixedTimeEqualsHexAcceptsUppercaseHex()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key").ToUpperInvariant();

        Assert.True(ApiKeyHasher.FixedTimeEqualsHex(hash, hash.ToLowerInvariant()));
    }

    [Fact]
    public void FixedTimeEqualsHexThrowsForNullArguments()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");

        Assert.Throws<ArgumentNullException>(() => ApiKeyHasher.FixedTimeEqualsHex(null!, hash));
        Assert.Throws<ArgumentNullException>(() => ApiKeyHasher.FixedTimeEqualsHex(hash, null!));
    }

    [Fact]
    public void FixedTimeEqualsHexReturnsFalseForInvalidHexCharacter()
    {
        var hash = ApiKeyHasher.HashSha256Hex("key");
        var invalidHash = hash[..63] + "g";

        Assert.False(ApiKeyHasher.FixedTimeEqualsHex(hash, invalidHash));
    }
}

public sealed class TransformAllowlistExtendedTests
{
    [Fact]
    public void ValidateRejectsNullTransformEntry()
    {
        IReadOnlyDictionary<string, object?>[] transforms = [null!];

        var result = TransformAllowlist.Validate(transforms);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("is null", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsTransformWithoutRecognizedKey()
    {
        IReadOnlyDictionary<string, object?>[] transforms =
        [
            new Dictionary<string, object?> { ["Set"] = "/api" },
        ];

        var result = TransformAllowlist.Validate(transforms);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("does not contain a recognized transform key", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsDisallowedParameterKey()
    {
        IReadOnlyDictionary<string, object?>[] transforms =
        [
            new Dictionary<string, object?> { ["PathPrefix"] = "/api", ["EvilParam"] = "x" },
        ];

        var result = TransformAllowlist.Validate(transforms);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("disallowed key 'EvilParam'", StringComparison.Ordinal));
    }
}
