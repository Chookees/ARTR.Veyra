using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class ConfigurationActivationServiceTests
{
    [Fact]
    public async Task StartAsync_ActivatesValidConfiguration()
    {
        var options = CreateValidOptions();
        var configuration = BuildConfiguration();
        var monitor = new MutableOptionsMonitor<VeyraOptions>(options);
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(1, service.Generation);
        Assert.NotEqual("none", service.Fingerprint);
        Assert.False(service.IsLastKnownGoodActive);
        Assert.True(service.LastActivatedUtc > DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task Reload_RejectsInvalidConfigurationAndRetainsLastKnownGood()
    {
        var options = CreateValidOptions();
        var configuration = BuildConfiguration();
        var monitor = new MutableOptionsMonitor<VeyraOptions>(options);
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        var originalFingerprint = service.Fingerprint;
        var originalGeneration = service.Generation;

        monitor.Set(new VeyraOptions { Admin = new AdminOptions { PathBase = "/" } });
        ((IConfigurationRoot)configuration).Reload();

        Assert.Equal(originalGeneration, service.Generation);
        Assert.Equal(originalFingerprint, service.Fingerprint);
        Assert.True(service.IsLastKnownGoodActive);
    }

    [Fact]
    public async Task StartAsync_ThrowsWhenInitialConfigurationInvalid()
    {
        var configuration = BuildConfiguration();
        var monitor = new MutableOptionsMonitor<VeyraOptions>(
            new VeyraOptions { Admin = new AdminOptions { PathBase = "/" } });
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => service.StartAsync(CancellationToken.None));
    }

    private static VeyraOptions CreateValidOptions() => new()
    {
        Admin = new AdminOptions { PathBase = "/_veyra" },
    };

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }
}
