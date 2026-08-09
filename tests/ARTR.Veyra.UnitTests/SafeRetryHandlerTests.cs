using System.Net;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Host.Traffic;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class SafeRetryHandlerTests
{
    [Fact]
    public async Task DoesNotRetry_WhenDisabled()
    {
        var inner = new CountingHandler(HttpStatusCode.BadGateway);
        var handler = new SafeRetryHandler(CreateMonitor(new VeyraOptions()))
        {
            InnerHandler = inner,
        };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("http://127.0.0.1/");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task RetriesIdempotent_OnBadGateway()
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

        var inner = new CountingHandler(HttpStatusCode.BadGateway);
        var handler = new SafeRetryHandler(CreateMonitor(options)) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("http://127.0.0.1/");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task DoesNotRetry_WhenRequestHasBody()
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

        var inner = new CountingHandler(HttpStatusCode.BadGateway);
        var handler = new SafeRetryHandler(CreateMonitor(options)) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var content = new StringContent("body");
        using var response = await client.PostAsync("http://127.0.0.1/", content);
        Assert.Equal(1, inner.Calls);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private static IOptionsMonitor<VeyraOptions> CreateMonitor(VeyraOptions options) =>
        new StaticOptionsMonitor(options);

    private sealed class StaticOptionsMonitor(VeyraOptions value) : IOptionsMonitor<VeyraOptions>
    {
        public VeyraOptions CurrentValue => value;

        public VeyraOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<VeyraOptions, string?> listener) => null;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public CountingHandler(HttpStatusCode status) => _status = status;

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }
}
