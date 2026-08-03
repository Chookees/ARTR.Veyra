using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ARTR.Veyra.IntegrationTests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task AdminRequiresApiKeyWhenConfigured()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateApiKeyConfiguration());
        var client = factory.CreateClient();

        var unauthorized = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task AdminAcceptsValidApiKey()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateApiKeyConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Add("X-Api-Key", GatewayFactory.DemoApiKey);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminRejectsInvalidApiKey()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateApiKeyConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Add("X-Api-Key", "wrong-secret");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminAllowsAnonymousWhenAuthenticationDisabled()
    {
        var config = GatewayFactory.CreateDefaultConfiguration();
        config["ARTR:Veyra:Authentication:Enabled"] = "false";
        config["ARTR:Veyra:Admin:RequireAuthentication"] = "true";

        await using var factory = new GatewayFactory(config);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
