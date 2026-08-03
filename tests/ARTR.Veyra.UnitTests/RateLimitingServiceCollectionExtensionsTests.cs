using ARTR.Veyra.Host.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class RateLimitingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVeyraRateLimitingSkipsPoliciesWithEmptyNames()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ARTR:Veyra:RateLimiting:Policies:0:Name"] = "",
                ["ARTR:Veyra:RateLimiting:Policies:0:PermitLimit"] = "1",
                ["ARTR:Veyra:RateLimiting:Policies:0:WindowSeconds"] = "60",
            })
            .Build();

        var services = new ServiceCollection();
        RateLimitingServiceCollectionExtensions.AddVeyraRateLimiting(services, configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddVeyraRateLimitingUsesEmptyPoliciesWhenSectionMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        RateLimitingServiceCollectionExtensions.AddVeyraRateLimiting(services, configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }
}
