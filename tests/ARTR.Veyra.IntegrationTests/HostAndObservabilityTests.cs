using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ARTR.Veyra.Core.Correlation;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ARTR.Veyra.IntegrationTests;

public sealed class HostAndObservabilityTests
{
    [Fact]
    public async Task ConfigSummaryReturnsExpectedShape()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/config/summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(payload.TryGetProperty("admin", out _));
        Assert.True(payload.TryGetProperty("authentication", out _));
        Assert.True(payload.TryGetProperty("rateLimiting", out _));
        Assert.True(payload.TryGetProperty("observability", out _));
    }

    [Fact]
    public async Task ConfigSummaryIncludesConfigurationGeneration()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/config/summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var configuration = payload.GetProperty("configuration");
        Assert.True(configuration.GetProperty("generation").GetInt64() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(configuration.GetProperty("fingerprint").GetString()));
        Assert.False(configuration.GetProperty("lastKnownGoodActive").GetBoolean());
    }

    [Fact]
    public async Task CorrelationIdIsGeneratedWhenHeaderMissing()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/info", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationConstants.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
    }

    [Fact]
    public async Task PrometheusEndpointNotMappedWhenDisabled()
    {
        await using var factory = new GatewayFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OtlpExporterConfigurationDoesNotPreventStartup()
    {
        var config = GatewayFactory.CreateDefaultConfiguration();
        config["ARTR:Veyra:Observability:Otlp:Enabled"] = "true";
        config["ARTR:Veyra:Observability:Otlp:Endpoint"] = "http://127.0.0.1:4317";

        await using var factory = new GatewayFactory(config);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_veyra/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class JwtAuthenticationIntegrationTests
{
    [Fact]
    public async Task AdminAcceptsValidJwtSignedWithConfiguredSecret()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateJwtConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(GatewayFactory.JwtSigningKey, "veyra-test"));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminRejectsMalformedJwtWhenJwtEnabled()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateJwtConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            "not-a-valid-jwt");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 Unauthorized but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("VEYRA_AUTH_INVALID", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminRejectsJwtWithInvalidSignatureWhenJwtEnabled()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateJwtConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken("abcdefghijklmnopqrstuvwxyz012345", "veyra-test"));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 Unauthorized but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("VEYRA_AUTH_INVALID", body, StringComparison.Ordinal);
    }

    private static string CreateJwtToken(string signingKey, string issuer)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            claims: [new Claim(ClaimTypes.NameIdentifier, "jwt-user")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class PolicySchemeSelectionTests
{
    [Fact]
    public async Task DualAuthSelectsApiKeySchemeWhenApiKeyHeaderPresent()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateDualAuthConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Add("X-Api-Key", GatewayFactory.DemoApiKey);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DualAuthSelectsJwtSchemeWhenBearerHeaderPresent()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateDualAuthConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(GatewayFactory.JwtSigningKey, "veyra-test"));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task JwtOnlyAuthUsesJwtScheme()
    {
        await using var factory = new GatewayFactory(GatewayFactory.CreateJwtConfiguration());
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_veyra/info");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(GatewayFactory.JwtSigningKey, "veyra-test"));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationPolicyCanBeRegisteredWhenEnabled()
    {
        var config = GatewayFactory.CreateApiKeyConfiguration();
        config["ARTR:Veyra:Authorization:Enabled"] = "true";
        config["ARTR:Veyra:Authorization:Policies:reader:0"] = "read";

        await using var factory = new GatewayFactory(config);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", GatewayFactory.DemoApiKey);

        var response = await client.GetAsync("/_veyra/config/summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CreateJwtToken(string signingKey, string issuer)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            claims: [new Claim(ClaimTypes.NameIdentifier, "jwt-user")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
