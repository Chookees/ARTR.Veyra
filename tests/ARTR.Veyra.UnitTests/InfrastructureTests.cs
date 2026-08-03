using ARTR.Veyra.Infrastructure.RateLimiting;
using ARTR.Veyra.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class EnvironmentSecretResolverTests
{
    private readonly EnvironmentSecretResolver _resolver = new();

    [Fact]
    public async Task ResolveAsyncReturnsEnvironmentVariableForEnvPrefix()
    {
        const string variableName = "VEYRA_TEST_ENV_SECRET";
        const string expectedValue = "secret-from-env";
        Environment.SetEnvironmentVariable(variableName, expectedValue);

        try
        {
            var result = await _resolver.ResolveAsync($"env:{variableName}", TestContext.Current.CancellationToken);

            Assert.Equal(expectedValue, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task ResolveAsyncReturnsEnvironmentVariableForPlainName()
    {
        const string variableName = "VEYRA_TEST_PLAIN_SECRET";
        const string expectedValue = "plain-secret";
        Environment.SetEnvironmentVariable(variableName, expectedValue);

        try
        {
            var result = await _resolver.ResolveAsync(variableName, TestContext.Current.CancellationToken);

            Assert.Equal(expectedValue, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenEnvironmentVariableMissing()
    {
        var result = await _resolver.ResolveAsync("env:VEYRA_TEST_MISSING_SECRET", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenEnvPrefixHasEmptyVariableName()
    {
        var result = await _resolver.ResolveAsync("env:", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}

public sealed class ConfigurationSecretResolverTests
{
    [Fact]
    public async Task ResolveAsyncReturnsConfigurationValueForConfigPrefix()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ARTR:Veyra:Secrets:SigningKey"] = "config-secret",
            })
            .Build();

        var resolver = new ConfigurationSecretResolver(configuration);

        var result = await resolver.ResolveAsync("config:ARTR:Veyra:Secrets:SigningKey", TestContext.Current.CancellationToken);

        Assert.Equal("config-secret", result);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullForNonConfigPrefix()
    {
        var configuration = new ConfigurationBuilder().Build();
        var resolver = new ConfigurationSecretResolver(configuration);

        var result = await resolver.ResolveAsync("plain-name", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenConfigurationKeyMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var resolver = new ConfigurationSecretResolver(configuration);

        var result = await resolver.ResolveAsync("config:Missing:Key", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenConfigPrefixHasEmptyPath()
    {
        var configuration = new ConfigurationBuilder().Build();
        var resolver = new ConfigurationSecretResolver(configuration);

        var result = await resolver.ResolveAsync("config:", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncUsesPlainPathWhenConfigPrefixMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PlainKey"] = "plain-value" })
            .Build();
        var resolver = new ConfigurationSecretResolver(configuration);

        var result = await resolver.ResolveAsync("PlainKey", TestContext.Current.CancellationToken);

        Assert.Equal("plain-value", result);
    }

    [Fact]
    public void ConstructorThrowsWhenConfigurationNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationSecretResolver(null!));
    }
}

public sealed class FileSecretResolverTests
{
    [Fact]
    public async Task ResolveAsyncReturnsTrimmedFileContents()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"veyra-secret-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(filePath, "  file-secret-value  \n", TestContext.Current.CancellationToken);

        try
        {
            var resolver = new FileSecretResolver();
            var result = await resolver.ResolveAsync($"file:{filePath}", TestContext.Current.CancellationToken);

            Assert.Equal("file-secret-value", result);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullForNonFilePrefix()
    {
        var resolver = new FileSecretResolver();

        var result = await resolver.ResolveAsync("env:NAME", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenFilePrefixHasEmptyPath()
    {
        var resolver = new FileSecretResolver();

        var result = await resolver.ResolveAsync("file:", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsyncThrowsWhenFileDoesNotExist()
    {
        var resolver = new FileSecretResolver();
        var secretName = $"file:{Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))}";

        await Assert.ThrowsAsync<ARTR.Veyra.Core.Secrets.SecretResolutionException>(
            () => resolver.ResolveAsync(secretName, TestContext.Current.CancellationToken));
    }
}

public sealed class CompositeSecretResolverTests
{
    [Fact]
    public async Task ResolveAsyncReturnsFirstMatchingResolverValue()
    {
        const string variableName = "VEYRA_TEST_COMPOSITE_SECRET";
        Environment.SetEnvironmentVariable(variableName, "composite-env-value");

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secrets:Key"] = "composite-config-value",
                })
                .Build();

            var resolver = new CompositeSecretResolver(
            [
                new EnvironmentSecretResolver(),
                new ConfigurationSecretResolver(configuration),
                new FileSecretResolver(),
            ]);

            var envResult = await resolver.ResolveAsync($"env:{variableName}", TestContext.Current.CancellationToken);
            var configResult = await resolver.ResolveAsync("config:Secrets:Key", TestContext.Current.CancellationToken);

            Assert.Equal("composite-env-value", envResult);
            Assert.Equal("composite-config-value", configResult);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task ResolveAsyncReturnsNullWhenNoResolverMatches()
    {
        var resolver = new CompositeSecretResolver(
        [
            new EnvironmentSecretResolver(),
            new ConfigurationSecretResolver(new ConfigurationBuilder().Build()),
            new FileSecretResolver(),
        ]);

        var result = await resolver.ResolveAsync("unknown:secret", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public void ConstructorThrowsWhenResolversNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositeSecretResolver(null!));
    }

    [Fact]
    public void ConstructorThrowsWhenResolversEmpty()
    {
        Assert.Throws<ArgumentException>(() => new CompositeSecretResolver([]));
    }
}

public sealed class MemoryRateLimiterStoreTests
{
    [Fact]
    public async Task TryAcquireAsyncAllowsRequestsUpToPermitLimit()
    {
        var store = new MemoryRateLimiterStore();
        const string key = "client-a";
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await store.TryAcquireAsync(key, permitLimit: 2, TimeSpan.FromMinutes(1), cancellationToken));
        Assert.True(await store.TryAcquireAsync(key, permitLimit: 2, TimeSpan.FromMinutes(1), cancellationToken));
        Assert.False(await store.TryAcquireAsync(key, permitLimit: 2, TimeSpan.FromMinutes(1), cancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsyncUsesIndependentKeys()
    {
        var store = new MemoryRateLimiterStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await store.TryAcquireAsync("client-a", permitLimit: 1, TimeSpan.FromMinutes(1), cancellationToken));
        Assert.True(await store.TryAcquireAsync("client-b", permitLimit: 1, TimeSpan.FromMinutes(1), cancellationToken));
        Assert.False(await store.TryAcquireAsync("client-a", permitLimit: 1, TimeSpan.FromMinutes(1), cancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsyncResetsAfterWindowExpires()
    {
        var store = new MemoryRateLimiterStore();
        const string key = "client-window";
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await store.TryAcquireAsync(key, permitLimit: 1, TimeSpan.FromMilliseconds(50), cancellationToken));
        Assert.False(await store.TryAcquireAsync(key, permitLimit: 1, TimeSpan.FromMilliseconds(50), cancellationToken));

        await Task.Delay(60, cancellationToken);

        Assert.True(await store.TryAcquireAsync(key, permitLimit: 1, TimeSpan.FromMilliseconds(50), cancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsyncThrowsForInvalidArguments()
    {
        var store = new MemoryRateLimiterStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.TryAcquireAsync("", permitLimit: 1, TimeSpan.FromMinutes(1), cancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.TryAcquireAsync("key", permitLimit: 0, TimeSpan.FromMinutes(1), cancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.TryAcquireAsync("key", permitLimit: 1, TimeSpan.Zero, cancellationToken).AsTask());
    }

    [Fact]
    public async Task TryAcquireAsyncHonorsCancellation()
    {
        var store = new MemoryRateLimiterStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.TryAcquireAsync("key", permitLimit: 1, TimeSpan.FromMinutes(1), cts.Token).AsTask());
    }
}
