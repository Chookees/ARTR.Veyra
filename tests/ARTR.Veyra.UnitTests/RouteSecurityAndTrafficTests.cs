using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Transforms;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class RouteSecurityAndTrafficTests
{
    [Fact]
    public void DenyByDefault_RequiresAllowAnonymousOrPolicy()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:r1:ClusterId"] = "c1",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = Bind(new Dictionary<string, string?>
        {
            ["Authentication:Enabled"] = "true",
            ["RoutingSecurity:DenyAnonymousRoutesByDefault"] = "true",
        });

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("AllowAnonymous", StringComparison.Ordinal));
    }

    [Fact]
    public void AllowAnonymous_PassesDenyByDefault()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:r1:ClusterId"] = "c1",
            ["ReverseProxy:Routes:r1:Metadata:AllowAnonymous"] = "true",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = Bind(new Dictionary<string, string?>
        {
            ["Authentication:Enabled"] = "true",
            ["RoutingSecurity:DenyAnonymousRoutesByDefault"] = "true",
        });

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Empty(failures);
    }

    [Fact]
    public void CanaryWeights_MustSumTo100()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:cluster-canary:Destinations:stable:Address"] = "http://127.0.0.1:1/",
            ["ReverseProxy:Clusters:cluster-canary:Destinations:canary:Address"] = "http://127.0.0.1:2/",
        }).Build();

        var options = new VeyraOptions
        {
            Canary = new CanaryOptions
            {
                Enabled = true,
                Splits =
                [
                    new CanarySplitOptions
                    {
                        Name = "default",
                        ClusterId = "cluster-canary",
                        Weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["stable"] = 80,
                            ["canary"] = 10,
                        },
                    },
                ],
            },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("sum to 100", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownRateLimitPolicy_IsRejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:r1:Metadata:RateLimitPolicy"] = "missing",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = new VeyraOptions
        {
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies =
                [
                    new RateLimitPolicyOptions
                    {
                        Name = "strict",
                        PermitLimit = 10,
                        WindowSeconds = 60,
                    },
                ],
            },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("unknown RateLimitPolicy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SafeRetries_RejectNonIdempotentMethods()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 2,
                    TotalTimeoutSeconds = 10,
                    IdempotentMethods = ["POST"],
                },
            },
        };

        var result = new VeyraOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("non-idempotent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Hedging_Enabled_IsRejected()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                Hedging = new HedgingOptions { Enabled = true },
            },
        };

        var result = new VeyraOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Hedging", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TransformAllowlist_AcceptsPathPattern()
    {
        var transforms = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["PathPattern"] = "/{**catch-all}" },
        };
        var result = TransformAllowlist.Validate(transforms, TransformAllowlist.DefaultAllowlist);
        Assert.True(result.IsValid);
    }

    private static VeyraOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return configuration.Get<VeyraOptions>() ?? new VeyraOptions();
    }
}
