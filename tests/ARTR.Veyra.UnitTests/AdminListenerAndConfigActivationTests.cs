using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class AdminListenerAndConfigActivationTests
{
    [Fact]
    public void ParseListenPortsExtractsPorts()
    {
        var ports = AdminOptions.ParseListenPorts("http://127.0.0.1:5081;https://127.0.0.1:5443");
        Assert.Contains(5081, ports);
        Assert.Contains(5443, ports);
    }

    [Fact]
    public void ValidatorRejectsInvalidAdminListenUrls()
    {
        var validator = new VeyraOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", ListenUrls = "not-a-url" },
        });
        Assert.True(result.Failed);
    }

    [Fact]
    public async Task ConfigurationActivationServiceActivatesValidOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var options = new VeyraOptions
        {
            Admin = new AdminOptions { PathBase = "/_veyra", RequireAuthentication = false },
            ConfigurationReload = new ConfigurationReloadOptions { Enabled = true, RetainLastKnownGood = true },
        };
        var monitor = new StaticOptionsMonitor(options);
        var service = new ConfigurationActivationService(
            configuration,
            monitor,
            new VeyraOptionsValidator(),
            NullLogger<ConfigurationActivationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        Assert.True(service.Generation >= 1);
        Assert.False(string.IsNullOrWhiteSpace(service.Fingerprint));
        Assert.False(service.IsLastKnownGoodActive);
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<VeyraOptions>
    {
        public StaticOptionsMonitor(VeyraOptions current) => CurrentValue = current;

        public VeyraOptions CurrentValue { get; }

        public VeyraOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<VeyraOptions, string?> listener) => null;
    }
}
