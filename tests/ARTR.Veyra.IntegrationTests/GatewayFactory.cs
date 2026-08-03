using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ARTR.Veyra.IntegrationTests;

public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    public const string DemoApiKeyHash = "cd577fe2561ebff23505db0bb006300c7cdecbd46bc0e03c449afafaca2c25bf";
    public const string DemoApiKey = "demo-secret";

    private readonly Dictionary<string, string?> _configuration;

    public GatewayFactory(Dictionary<string, string?>? configuration = null)
    {
        _configuration = configuration ?? CreateDefaultConfiguration();
    }

    public GatewayFactory(Action<IConfigurationBuilder> configure)
    {
        _configuration = CreateDefaultConfiguration();
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(_configuration);
        configure(builder);
        _configuration = builder.Build().AsEnumerable()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, string?> CreateDefaultConfiguration() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Urls"] = "http://127.0.0.1:0",
        ["ARTR:Veyra:Admin:Enabled"] = "true",
        ["ARTR:Veyra:Admin:PathBase"] = "/_veyra",
        ["ARTR:Veyra:Admin:RequireAuthentication"] = "false",
        ["ARTR:Veyra:Authentication:Enabled"] = "false",
        ["ARTR:Veyra:RateLimiting:Enabled"] = "false",
        ["ARTR:Veyra:Health:Enabled"] = "true",
        ["ARTR:Veyra:Health:LivePath"] = "/health/live",
        ["ARTR:Veyra:Health:ReadyPath"] = "/health/ready",
        ["ARTR:Veyra:Health:StartupPath"] = "/health/startup",
        ["ARTR:Veyra:Observability:ConsoleLogging"] = "false",
        ["ARTR:Veyra:Observability:Prometheus:Enabled"] = "false",
        ["ARTR:Veyra:Observability:Otlp:Enabled"] = "false",
        ["ReverseProxy:Routes:upstream-a:ClusterId"] = "cluster-a",
        ["ReverseProxy:Routes:upstream-a:Match:Path"] = "/a/{**catch-all}",
        ["ReverseProxy:Routes:upstream-a:Transforms:0:PathPattern"] = "/{**catch-all}",
        ["ReverseProxy:Clusters:cluster-a:Destinations:d1:Address"] = "http://127.0.0.1:59999/",
    };

    public static Dictionary<string, string?> CreateApiKeyConfiguration()
    {
        var config = CreateDefaultConfiguration();
        config["ARTR:Veyra:Authentication:Enabled"] = "true";
        config["ARTR:Veyra:Authentication:ApiKey:Enabled"] = "true";
        config["ARTR:Veyra:Authentication:ApiKey:HeaderName"] = "X-Api-Key";
        config["ARTR:Veyra:Authentication:ApiKey:Keys:0:Id"] = "demo-key";
        config["ARTR:Veyra:Authentication:ApiKey:Keys:0:Name"] = "demo";
        config["ARTR:Veyra:Authentication:ApiKey:Keys:0:HashSha256Hex"] = DemoApiKeyHash;
        config["ARTR:Veyra:Authentication:ApiKey:Keys:0:Roles:0"] = "admin";
        config["ARTR:Veyra:Admin:RequireAuthentication"] = "true";
        return config;
    }

    public const string JwtSigningKeyVariable = "VEYRA_TEST_JWT_SIGNING_KEY";

    public const string JwtSigningKey = "01234567890123456789012345678901";

    public const string JwtSigningKeyConfigPath = "ARTR:Veyra:TestSecrets:JwtSigningKey";

    public static Dictionary<string, string?> CreateJwtConfiguration()
    {
        var config = CreateDefaultConfiguration();
        config["ARTR:Veyra:Authentication:Enabled"] = "true";
        config["ARTR:Veyra:Authentication:Jwt:Enabled"] = "true";
        config["ARTR:Veyra:Authentication:Jwt:Issuer"] = "veyra-test";
        // Prefer config: secrets in tests — avoids parallel env-var races on CI.
        config[JwtSigningKeyConfigPath] = JwtSigningKey;
        config["ARTR:Veyra:Authentication:Jwt:SigningKeySecretName"] = $"config:{JwtSigningKeyConfigPath}";
        config["ARTR:Veyra:Admin:RequireAuthentication"] = "true";
        return config;
    }

    public static Dictionary<string, string?> CreateDualAuthConfiguration()
    {
        var config = CreateApiKeyConfiguration();
        config["ARTR:Veyra:Authentication:Jwt:Enabled"] = "true";
        config["ARTR:Veyra:Authentication:Jwt:Issuer"] = "veyra-test";
        config[JwtSigningKeyConfigPath] = JwtSigningKey;
        config["ARTR:Veyra:Authentication:Jwt:SigningKeySecretName"] = $"config:{JwtSigningKeyConfigPath}";
        return config;
    }

    public WebApplicationFactory<Program> WithWebHostConfiguration(Action<IWebHostBuilder> configure) =>
        WithWebHostBuilder(configure);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(_configuration);
        });
    }
}
