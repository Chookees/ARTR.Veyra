using BenchmarkDotNet.Attributes;
using ARTR.Veyra.Core.Security;

namespace ARTR.Veyra.Benchmarks;

[MemoryDiagnoser]
public class ApiKeyHashBenchmarks
{
    private string _key = "demo-secret";

    [GlobalSetup]
    public void Setup() => _key = "demo-secret";

    [Benchmark]
    public string HashDemoSecret() => ApiKeyHasher.HashSha256Hex(_key);
}
