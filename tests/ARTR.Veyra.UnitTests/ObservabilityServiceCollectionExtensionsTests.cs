using ARTR.Veyra.Observability.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class ObservabilityServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVeyraObservabilityRegistersOpenTelemetryWithPrometheusAndOtlp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ARTR:Veyra:Observability:ServiceName"] = "veyra-test",
                ["ARTR:Veyra:Observability:Prometheus:Enabled"] = "true",
                ["ARTR:Veyra:Observability:Otlp:Enabled"] = "true",
                ["ARTR:Veyra:Observability:Otlp:Endpoint"] = "http://127.0.0.1:4317",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddVeyraObservability(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<Microsoft.Extensions.Hosting.IHostedService>());
    }

    [Fact]
    public void AddVeyraObservabilityUsesDefaultsWhenSectionMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddVeyraObservability(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<Microsoft.Extensions.Hosting.IHostedService>());
    }
}
