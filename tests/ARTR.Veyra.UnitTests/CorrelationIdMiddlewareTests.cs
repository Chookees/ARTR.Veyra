using System.Net;
using ARTR.Veyra.Core.Correlation;
using ARTR.Veyra.Observability.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsyncGeneratesCorrelationIdWhenHeaderMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(context.TraceIdentifier));
        Assert.Equal(context.TraceIdentifier, context.Items[CorrelationConstants.HeaderName]);
    }

    [Fact]
    public async Task InvokeAsyncGeneratesCorrelationIdWhenHeaderWhitespace()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationConstants.HeaderName] = "   ";
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(context.TraceIdentifier));
    }

    [Fact]
    public async Task InvokeAsyncTrimsExistingCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationConstants.HeaderName] = "  existing-id  ";
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("existing-id", context.TraceIdentifier);
        Assert.Equal("existing-id", context.Items[CorrelationConstants.HeaderName]);
    }

    [Fact]
    public void ConstructorThrowsWhenNextDelegateNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CorrelationIdMiddleware(null!, NullLogger<CorrelationIdMiddleware>.Instance));
    }

    [Fact]
    public void ConstructorThrowsWhenLoggerNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CorrelationIdMiddleware(_ => Task.CompletedTask, null!));
    }
}
