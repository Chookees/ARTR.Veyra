using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARTR.Veyra.Core.Correlation;
using Xunit;

namespace ARTR.Veyra.FunctionalTests;

public sealed class RoutingFunctionalTests
{
    [Fact]
    public async Task ProxiedRequestRoundTripsCorrelationHeader()
    {
        await using var upstream = await LoopbackUpstreamHost.StartAsync(TestContext.Current.CancellationToken);
        await using var factory = new FunctionalGatewayFactory(upstream.BaseAddress);
        using var client = factory.CreateClient();

        const string correlationId = "test-correlation-abc123";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/a/hello");
        request.Headers.Add(CorrelationConstants.HeaderName, correlationId);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationConstants.HeaderName, out var responseHeaders));
        Assert.Equal(correlationId, responseHeaders.Single());

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(correlationId, json.GetProperty("correlationId").GetString());
        Assert.Equal("Hello from upstream", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RateLimitReturns429WhenPermitLimitExceeded()
    {
        var config = ARTR.Veyra.IntegrationTests.GatewayFactory.CreateDefaultConfiguration();
        config["ARTR:Veyra:RateLimiting:Enabled"] = "true";
        config["ARTR:Veyra:RateLimiting:GlobalPermitLimit"] = "1";
        config["ARTR:Veyra:RateLimiting:GlobalWindowSeconds"] = "60";

        await using var factory = new FunctionalGatewayFactory("http://127.0.0.1:59999/", config);
        using var client = factory.CreateClient();

        var first = await client.GetAsync("/_veyra/health/live", TestContext.Current.CancellationToken);
        var second = await client.GetAsync("/_veyra/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("VEYRA_RATE_LIMITED", body, StringComparison.Ordinal);
    }
}
