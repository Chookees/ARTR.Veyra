using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Extensions;
using ARTR.Veyra.Core.Plugins;
using ARTR.Veyra.Core.Transforms;
using ARTR.Veyra.Host.Traffic;
using ARTR.Veyra.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace ARTR.Veyra.UnitTests;

public sealed class AscensionCoverageTests
{
    [Fact]
    public void GateResult_AllowAndDeny()
    {
        Assert.True(VeyraGateResult.Allow().Allowed);
        var deny = VeyraGateResult.Deny(403, "nope");
        Assert.False(deny.Allowed);
        Assert.Equal(403, deny.StatusCode);
        Assert.Equal("nope", deny.Detail);
    }

    [Fact]
    public void RouteSecurity_CoversPoliciesWeightsAndHeaders()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:secure:ClusterId"] = "c1",
            ["ReverseProxy:Routes:secure:Metadata:AuthorizationPolicy"] = "VeyraAdmin",
            ["ReverseProxy:Routes:limited:ClusterId"] = "c1",
            ["ReverseProxy:Routes:limited:Metadata:AllowAnonymous"] = "true",
            ["ReverseProxy:Routes:limited:Metadata:RateLimitPolicy"] = "strict",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Metadata:Weight"] = "50",
            ["ReverseProxy:Clusters:c1:Destinations:d2:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:c1:Destinations:d2:Metadata:Weight"] = "50",
            ["ReverseProxy:Clusters:zero:Destinations:z1:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:zero:Destinations:z1:Metadata:Weight"] = "0",
            ["ReverseProxy:Clusters:zero:Destinations:z2:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:zero:Destinations:z2:Metadata:Weight"] = "0",
            ["ReverseProxy:Clusters:cluster-canary:Destinations:stable:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:cluster-canary:Destinations:canary:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions { Enabled = true },
            Authorization = new AuthorizationOptions { Enabled = true },
            RoutingSecurity = new RoutingSecurityOptions { DenyAnonymousRoutesByDefault = true },
            RateLimiting = new RateLimitingOptions
            {
                Enabled = true,
                Policies = [new RateLimitPolicyOptions { Name = "strict", PermitLimit = 1, WindowSeconds = 1 }],
            },
            TrafficEngineering = new TrafficEngineeringOptions { ValidateClusterWeights = true },
            Canary = new CanaryOptions
            {
                Enabled = true,
                Splits =
                [
                    new CanarySplitOptions
                    {
                        Name = "default",
                        ClusterId = "cluster-canary",
                        Weights = new Dictionary<string, int> { ["stable"] = 90, ["canary"] = 10 },
                        MatchHeaderName = "X-Canary",
                        MatchHeaderValue = "1",
                    },
                ],
            },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("all weights are zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RouteSecurity_UnknownPolicyAndIncompleteHeader()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:r1:Metadata:AuthorizationPolicy"] = "missing-policy",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:cluster-canary:Destinations:stable:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:cluster-canary:Destinations:canary:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions { Enabled = true },
            Authorization = new AuthorizationOptions { Enabled = true },
            RoutingSecurity = new RoutingSecurityOptions { DenyAnonymousRoutesByDefault = true },
            Canary = new CanaryOptions
            {
                Enabled = true,
                Splits =
                [
                    new CanarySplitOptions
                    {
                        Name = "bad",
                        ClusterId = "cluster-canary",
                        Weights = new Dictionary<string, int> { ["stable"] = 50, ["canary"] = 50 },
                        MatchHeaderName = "X-Only-Name",
                    },
                ],
            },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("unknown AuthorizationPolicy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(failures, f => f.Contains("MatchHeaderName", StringComparison.Ordinal));
    }

    [Fact]
    public void RouteSecurity_EmptyRoutesWithAuth()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions { Enabled = true },
            RoutingSecurity = new RoutingSecurityOptions { DenyAnonymousRoutesByDefault = true },
        };
        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("no ReverseProxy:Routes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OptionsValidator_TrafficAndPlugins()
    {
        var validator = new VeyraOptionsValidator();

        var badRetries = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 99,
                    TotalTimeoutSeconds = 999,
                    IdempotentMethods = [],
                },
                OutlierDetection = new OutlierDetectionOptions
                {
                    Enabled = true,
                    ConsecutiveFailureEjectionThreshold = 0,
                    EjectionDurationSeconds = 99999,
                },
            },
        };
        Assert.True(validator.Validate(null, badRetries).Failed);

        var badPlugins = new VeyraOptions
        {
            Features = new FeaturesOptions
            {
                Enabled = [""],
                Plugins = new PluginFeaturesOptions
                {
                    Enabled = true,
                    AllowedRoot = "",
                    Entries = [],
                },
            },
        };
        Assert.True(validator.Validate(null, badPlugins).Failed);

        var badPluginEntries = new VeyraOptions
        {
            Features = new FeaturesOptions
            {
                Plugins = new PluginFeaturesOptions
                {
                    Enabled = true,
                    AllowedRoot = "C:/plugins",
                    Entries =
                    [
                        new PluginEntryOptions { Id = "a", Path = "", Sha256Hex = "zz" },
                        new PluginEntryOptions { Id = "a", Path = "x", Sha256Hex = new string('a', 64) },
                    ],
                },
            },
        };
        Assert.True(validator.Validate(null, badPluginEntries).Failed);

        var canaryEnabledEmpty = new VeyraOptions
        {
            Canary = new CanaryOptions { Enabled = true },
        };
        Assert.True(validator.Validate(null, canaryEnabledEmpty).Failed);
    }

    [Fact]
    public void PluginLoader_RejectsMissingFileAndHashMismatch()
    {
        var allowed = Path.Combine(AppContext.BaseDirectory, "plugin-cov-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(allowed);
        try
        {
            var path = Path.Combine(allowed, "p.bin");
            File.WriteAllBytes(path, [9, 9, 9]);
            var loader = new Sha256PluginLoader();

            var missing = loader.Load(
                [new PluginDescriptor { Id = "m", Path = Path.Combine(allowed, "nope.bin"), Sha256Hex = new string('a', 64) }],
                allowed);
            Assert.Contains(missing, r => r.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase));

            var mismatch = loader.Load(
                [new PluginDescriptor { Id = "h", Path = path, Sha256Hex = new string('b', 64) }],
                allowed);
            Assert.Contains(mismatch, r => r.Error!.Contains("SHA-256", StringComparison.OrdinalIgnoreCase));

            var incomplete = loader.Load(
                [new PluginDescriptor { Id = "", Path = "", Sha256Hex = "" }],
                allowed);
            Assert.Contains(incomplete, r => !r.Success);

            var missingRoot = loader.Load([], Path.Combine(allowed, "does-not-exist"));
            Assert.Contains(missingRoot, r => r.Error!.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(allowed, recursive: true);
        }
    }

    [Fact]
    public async Task TrafficFilter_AppliesCanaryAndOutlier()
    {
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
                        ClusterId = "c1",
                        Weights = new Dictionary<string, int> { ["d1"] = 70, ["d2"] = 30 },
                    },
                ],
            },
            TrafficEngineering = new TrafficEngineeringOptions
            {
                OutlierDetection = new OutlierDetectionOptions
                {
                    Enabled = true,
                    EjectionDurationSeconds = 45,
                },
            },
        };

        var filter = new VeyraTrafficProxyConfigFilter(new StaticMonitor(options));
        var cluster = new ClusterConfig
        {
            ClusterId = "c1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["d1"] = new DestinationConfig { Address = "http://127.0.0.1:1/" },
                ["d2"] = new DestinationConfig { Address = "http://127.0.0.1:2/" },
                ["d3"] = new DestinationConfig { Address = "http://127.0.0.1:3/" },
            },
        };

        var updated = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);
        Assert.Equal("PowerOfTwoChoices", updated.LoadBalancingPolicy);
        Assert.True(updated.HealthCheck?.Passive?.Enabled);
        Assert.Equal("70", updated.Destinations!["d1"].Metadata!["Weight"]);
        Assert.Equal("30", updated.Destinations["d2"].Metadata!["Weight"]);

        var route = await filter.ConfigureRouteAsync(
            new RouteConfig { RouteId = "r1", ClusterId = "c1" },
            updated,
            CancellationToken.None);
        Assert.Equal("r1", route.RouteId);
    }

    [Fact]
    public void SafeRetryFactory_WrapsWhenEnabled()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions { Enabled = true, MaxAttempts = 2, IdempotentMethods = ["GET"] },
            },
        };
        var factory = new SafeRetryForwarderHttpClientFactory(new StaticMonitor(options));
        using var invoker = factory.CreateClient(new ForwarderHttpClientContext
        {
            NewConfig = new HttpClientConfig(),
            OldConfig = new HttpClientConfig(),
        });
        Assert.NotNull(invoker);

        var disabled = new SafeRetryForwarderHttpClientFactory(new StaticMonitor(new VeyraOptions()));
        using var invoker2 = disabled.CreateClient(new ForwarderHttpClientContext
        {
            NewConfig = new HttpClientConfig(),
            OldConfig = new HttpClientConfig(),
        });
        Assert.NotNull(invoker2);
    }

    [Fact]
    public async Task SafeRetry_RetriesOnTransportFailureThenSucceeds()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 3,
                    TotalTimeoutSeconds = 30,
                    IdempotentMethods = ["GET"],
                },
            },
        };

        var inner = new FlakyHandler(failTimes: 2);
        var handler = new SafeRetryHandler(new StaticMonitor(options)) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("http://127.0.0.1/");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task SafeRetry_RetriesServiceUnavailableThenSucceeds()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 3,
                    TotalTimeoutSeconds = 30,
                    IdempotentMethods = ["GET", "HEAD"],
                },
            },
        };

        var inner = new SequenceHandler(
            System.Net.HttpStatusCode.ServiceUnavailable,
            System.Net.HttpStatusCode.GatewayTimeout,
            System.Net.HttpStatusCode.OK);
        var handler = new SafeRetryHandler(new StaticMonitor(options)) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("http://127.0.0.1/");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task SafeRetry_ExhaustedTransportErrors_Throws()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 2,
                    TotalTimeoutSeconds = 30,
                    IdempotentMethods = ["GET"],
                },
            },
        };

        var inner = new AlwaysThrowHandler();
        var handler = new SafeRetryHandler(new StaticMonitor(options)) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1/"));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task SafeRetry_HeadWithoutBody_IsRetried()
    {
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 2,
                    TotalTimeoutSeconds = 30,
                    IdempotentMethods = ["HEAD"],
                },
            },
        };

        var inner = new SequenceHandler(
            System.Net.HttpStatusCode.BadGateway,
            System.Net.HttpStatusCode.OK);
        var handler = new SafeRetryHandler(new StaticMonitor(options)) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Head, "http://127.0.0.1/");
        using var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    private sealed class SequenceHandler(params System.Net.HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var status = statuses[Math.Min(Calls, statuses.Length - 1)];
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class AlwaysThrowHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new HttpRequestException("down");
        }
    }

    private sealed class FlakyHandler(int failTimes) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failTimes)
            {
                throw new HttpRequestException("transient");
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    [Fact]
    public void RouteSecurity_NegativeWeightAndUnknownCanaryDestination()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Metadata:Weight"] = "-1",
            ["ReverseProxy:Clusters:cluster-canary:Destinations:stable:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions { ValidateClusterWeights = true },
            Canary = new CanaryOptions
            {
                Enabled = true,
                Splits =
                [
                    new CanarySplitOptions
                    {
                        Name = "dup",
                        ClusterId = "missing",
                        Weights = new Dictionary<string, int> { ["stable"] = 100 },
                    },
                    new CanarySplitOptions
                    {
                        Name = "dup",
                        ClusterId = "cluster-canary",
                        Weights = new Dictionary<string, int> { ["ghost"] = 100 },
                    },
                    new CanarySplitOptions
                    {
                        Name = "empty",
                        ClusterId = "cluster-canary",
                        Weights = new Dictionary<string, int>(),
                    },
                    new CanarySplitOptions
                    {
                        Name = "oor",
                        ClusterId = "cluster-canary",
                        Weights = new Dictionary<string, int> { ["stable"] = 101 },
                    },
                ],
            },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.NotEmpty(failures);
    }

    [Fact]
    public void PluginLoader_CatchPath_WhenPathIsDirectory()
    {
        var allowed = Path.Combine(AppContext.BaseDirectory, "plugin-dir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(allowed);
        try
        {
            var loader = new Sha256PluginLoader();
            var results = loader.Load(
                [new PluginDescriptor { Id = "dir", Path = allowed, Sha256Hex = new string('a', 64) }],
                allowed);
            Assert.Contains(results, r => !r.Success);
        }
        finally
        {
            Directory.Delete(allowed, recursive: true);
        }
    }

    [Fact]
    public void RouteSecurity_UnknownPolicyIgnoredWhenAuthorizationDisabled()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:r1:Metadata:AuthorizationPolicy"] = "missing-policy",
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
        }).Build();

        var options = new VeyraOptions
        {
            Authentication = new AuthenticationOptions { Enabled = true },
            Authorization = new AuthorizationOptions { Enabled = false },
            RoutingSecurity = new RoutingSecurityOptions { DenyAnonymousRoutesByDefault = true },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.DoesNotContain(
            failures,
            f => f.Contains("unknown AuthorizationPolicy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RouteSecurity_CanaryEnabledWithoutSplits()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var options = new VeyraOptions { Canary = new CanaryOptions { Enabled = true, Splits = [] } };
        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("at least one split", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RouteSecurity_EmptyCanaryName()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "http://127.0.0.1/",
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
                        Name = " ",
                        ClusterId = "c1",
                        Weights = new Dictionary<string, int> { ["d1"] = 100 },
                    },
                ],
            },
        };

        var failures = ReverseProxyRouteSecurityValidator.Validate(config, options);
        Assert.Contains(failures, f => f.Contains("unique non-empty Name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TransformAllowlist_RejectsNullAndMultiKey()
    {
        var transforms = new List<IReadOnlyDictionary<string, object?>>
        {
            null!,
            new Dictionary<string, object?> { ["PathPattern"] = "/x", ["PathPrefix"] = "/y" },
            new Dictionary<string, object?> { ["Evil"] = "1" },
        };
        var result = TransformAllowlist.Validate(transforms, TransformAllowlist.DefaultAllowlist);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task TrafficFilter_NoopWhenFeaturesDisabled()
    {
        var filter = new VeyraTrafficProxyConfigFilter(new StaticMonitor(new VeyraOptions()));
        var cluster = new ClusterConfig
        {
            ClusterId = "c1",
            LoadBalancingPolicy = "RoundRobin",
            Destinations = null,
            HealthCheck = new HealthCheckConfig
            {
                Passive = new PassiveHealthCheckConfig
                {
                    Enabled = false,
                    Policy = "Custom",
                },
            },
        };

        var updated = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);
        Assert.Equal("RoundRobin", updated.LoadBalancingPolicy);
        Assert.False(updated.HealthCheck?.Passive?.Enabled);
    }

    [Fact]
    public async Task TrafficFilter_PreservesExistingDestinationMetadata()
    {
        var options = new VeyraOptions
        {
            Canary = new CanaryOptions
            {
                Enabled = true,
                Splits =
                [
                    new CanarySplitOptions
                    {
                        Name = "other",
                        ClusterId = "other-cluster",
                        Weights = new Dictionary<string, int> { ["x"] = 100 },
                    },
                ],
            },
        };

        var filter = new VeyraTrafficProxyConfigFilter(new StaticMonitor(options));
        var cluster = new ClusterConfig
        {
            ClusterId = "c1",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new DestinationConfig
                {
                    Address = "http://127.0.0.1/",
                    Metadata = new Dictionary<string, string> { ["Weight"] = "5" },
                },
            },
        };

        var updated = await filter.ConfigureClusterAsync(cluster, CancellationToken.None);
        Assert.Equal("5", updated.Destinations!["d1"].Metadata!["Weight"]);
        Assert.Equal("PowerOfTwoChoices", updated.LoadBalancingPolicy);
    }

    [Fact]
    public void OptionsValidator_OutlierBounds()
    {
        var validator = new VeyraOptionsValidator();
        var options = new VeyraOptions
        {
            TrafficEngineering = new TrafficEngineeringOptions
            {
                OutlierDetection = new OutlierDetectionOptions
                {
                    Enabled = true,
                    ConsecutiveFailureEjectionThreshold = 0,
                    EjectionDurationSeconds = 0,
                },
                SafeRetries = new SafeRetryOptions
                {
                    Enabled = true,
                    MaxAttempts = 0,
                    TotalTimeoutSeconds = 0,
                    IdempotentMethods = ["GET", ""],
                },
            },
        };

        var result = validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    private sealed class StaticMonitor(VeyraOptions value) : IOptionsMonitor<VeyraOptions>
    {
        public VeyraOptions CurrentValue => value;

        public VeyraOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<VeyraOptions, string?> listener) => null;
    }
}
