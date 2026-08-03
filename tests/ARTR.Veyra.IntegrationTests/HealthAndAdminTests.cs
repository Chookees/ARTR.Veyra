using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ARTR.Veyra.IntegrationTests;

public sealed class HealthAndAdminTests
{
    [Theory]
    [InlineData("/_veyra/health/live")]
    [InlineData("/_veyra/health/ready")]
    [InlineData("/_veyra/health/startup")]
    public async Task HealthEndpointsReturnOk(string path)
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InfoReturnsProductIdentity()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Equal("ARTR Veyra", payload["product"]?.ToString());
    }

    [Fact]
    public async Task InfoReturnsUnauthorizedWhenAdminRequiresAuthentication()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateApiKeyConfiguration());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InfoReturnsOkWhenAdminRequiresAuthenticationAndValidApiKeyProvided()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateApiKeyConfiguration());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", GatewayFactory.DemoApiKey);

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CorrelationHeaderIsEchoed()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Add("X-Correlation-ID", "test-correlation-123");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Contains("test-correlation-123", values);
    }
}
