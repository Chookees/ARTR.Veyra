using BenchmarkDotNet.Attributes;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Correlation;
using ARTR.Veyra.Core.Security;
using ARTR.Veyra.Core.Transforms;
using Microsoft.Extensions.Configuration;

namespace ARTR.Veyra.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public class GatewayHotPathBenchmarks
{
    private string _key = "demo-secret";
    private VeyraOptions _options = new();
    private IConfiguration _configuration = null!;
    private List<IReadOnlyDictionary<string, object?>> _transforms = null!;

    [GlobalSetup]
    public void Setup()
    {
        _key = "demo-secret";
        _options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies =
                [
                    new RateLimitPolicyOptions
                    {
                        Name = "strict",
                        PermitLimit = 60,
                        WindowSeconds = 60,
                    },
                ],
            },
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:r1:Metadata:AllowAnonymous"] = "true",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
        }).Build();
        _transforms =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["PathPattern"] = "/{**catch-all}",
            },
        ];
    }

    [Benchmark]
    public string ApiKeyHash() => ApiKeyHasher.HashSha256Hex(_key);

    [Benchmark]
    public string RateLimitPartitionKey()
    {
        var ip = "203.0.113.10";
        var policy = _options.RateLimiting.Policies[0].Name;
        return policy + ":" + ip;
    }

    [Benchmark]
    public string CorrelationId_NewGuid() => Guid.NewGuid().ToString("N");

    [Benchmark]
    public bool CorrelationHeader_IsPresent()
    {
        const string sample = "aabbccddeeff00112233445566778899";
        return !string.IsNullOrWhiteSpace(sample) &&
               sample.Length <= 128 &&
               string.Equals(CorrelationConstants.HeaderName, "X-Correlation-ID", StringComparison.Ordinal);
    }

    [Benchmark]
    public VeyraOptions OptionsLookup_Bind() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Enabled"] = "true",
                ["RateLimiting:GlobalPermitLimit"] = "100",
            })
            .Build()
            .Get<VeyraOptions>() ?? new VeyraOptions();

    [Benchmark]
    public bool TransformAllowlist_Validate() =>
        TransformAllowlist.Validate(_transforms, TransformAllowlist.DefaultAllowlist).IsValid;

    [Benchmark]
    public int RouteSecurity_Validate() =>
        ReverseProxyRouteSecurityValidator.Validate(_configuration, _options).Count;
}
