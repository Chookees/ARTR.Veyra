using System.Net;
using Xunit;

namespace ARTR.Veyra.IntegrationTests;

public sealed class AscensionFeatureTests
{
    [Fact]
    public async Task Diagnostics_ReturnsBoundedSummary()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/_veyra/diagnostics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("configuration", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clusters", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RouteRateLimit_Returns429()
    {
        var config = GatewayFactory.CreateDefaultConfiguration();
        config["ARTR:Veyra:RateLimiting:Enabled"] = "true";
        config["ARTR:Veyra:RateLimiting:GlobalPermitLimit"] = "10000";
        config["ARTR:Veyra:RateLimiting:GlobalWindowSeconds"] = "60";
        config["ARTR:Veyra:RateLimiting:Policies:0:Name"] = "strict";
        config["ARTR:Veyra:RateLimiting:Policies:0:PermitLimit"] = "2";
        config["ARTR:Veyra:RateLimiting:Policies:0:WindowSeconds"] = "60";
        config["ARTR:Veyra:RateLimiting:Policies:0:QueueLimit"] = "0";
        config["ReverseProxy:Routes:upstream-a:Metadata:RateLimitPolicy"] = "strict";

        await using var factory = new GatewayFactory(config);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/a/hello")).StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/a/hello")).StatusCode);
        var limited = await client.GetAsync("/a/hello");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public void DenyByDefault_FailsStartup_WithoutRouteMetadata()
    {
        var config = GatewayFactory.CreateApiKeyConfiguration();
        config.Remove("ReverseProxy:Routes:upstream-a:Metadata:AllowAnonymous");

        using var factory = new GatewayFactory(config);
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("route security", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
