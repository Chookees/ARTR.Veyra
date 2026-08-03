using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ARTR.Veyra.FunctionalTests;

internal sealed class FunctionalGatewayFactory : WebApplicationFactory<Program>
{
    private readonly string _upstreamAddress;
    private readonly Dictionary<string, string?> _configuration;

    public FunctionalGatewayFactory(string upstreamAddress, Dictionary<string, string?>? overrideConfiguration = null)
    {
        _upstreamAddress = upstreamAddress;
        _configuration = overrideConfiguration is null
            ? ARTR.Veyra.IntegrationTests.GatewayFactory.CreateDefaultConfiguration()
            : new Dictionary<string, string?>(overrideConfiguration, StringComparer.OrdinalIgnoreCase);

        _configuration["ReverseProxy:Clusters:cluster-a:Destinations:d1:Address"] = _upstreamAddress;
        _configuration["ARTR:Veyra:Observability:ConsoleLogging"] = "false";
        if (overrideConfiguration is null)
        {
            _configuration["ARTR:Veyra:Authentication:Enabled"] = "false";
            _configuration["ARTR:Veyra:RateLimiting:Enabled"] = "true";
        }
    }

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
