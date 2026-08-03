using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARTR.Veyra.Core.Correlation;
using Xunit;

namespace ARTR.Veyra.FunctionalTests;

public sealed class AdminFunctionalTests
{
    [Fact]
    public async Task InfoReturnsProductIdentity()
    {
        await using var factory = new FunctionalGatewayFactory("http://127.0.0.1:59999/");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ARTR Veyra", payload.GetProperty("product").GetString());
    }

    [Fact]
    public async Task CorrelationHeaderIsEchoedOnAdminRequest()
    {
        await using var factory = new FunctionalGatewayFactory("http://127.0.0.1:59999/");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Add(CorrelationConstants.HeaderName, "functional-correlation-456");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationConstants.HeaderName, out var values));
        Assert.Contains("functional-correlation-456", values);
    }

    [Fact]
    public async Task ConfigSummaryIncludesConfigurationFingerprint()
    {
        await using var factory = new FunctionalGatewayFactory("http://127.0.0.1:59999/");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/config/summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var configuration = payload.GetProperty("configuration");
        Assert.True(configuration.GetProperty("generation").GetInt64() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(configuration.GetProperty("fingerprint").GetString()));
    }

    [Fact]
    public async Task InfoReturnsUnauthorizedWhenApiKeyRequiredAndMissing()
    {
        var config = ARTR.Veyra.IntegrationTests.GatewayFactory.CreateApiKeyConfiguration();
        await using var factory = new FunctionalGatewayFactory("http://127.0.0.1:59999/", config);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/_veyra/health/live")]
    [InlineData("/_veyra/health/ready")]
    [InlineData("/_veyra/health/startup")]
    public async Task HealthEndpointsReturnOk(string path)
    {
        await using var factory = new FunctionalGatewayFactory("http://127.0.0.1:59999/");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
