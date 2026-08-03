using ARTR.Veyra.Core.Configuration;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class AdminListenerIsolationExtensionsTests
{
    [Fact]
    public void ParseListenPorts_ReturnsEmptySetForBlankInput()
    {
        var ports = AdminOptions.ParseListenPorts("   ");
        Assert.Empty(ports);
    }

    [Fact]
    public void ParseListenPorts_ReturnsEmptyForNull()
    {
        Assert.Empty(AdminOptions.ParseListenPorts(null));
    }

    [Fact]
    public void ParseListenPorts_ParsesSingleHttpUrl()
    {
        var ports = AdminOptions.ParseListenPorts("http://127.0.0.1:5081");
        Assert.Single(ports);
        Assert.Contains(5081, ports);
    }

    [Fact]
    public void ParseListenPorts_ParsesSemicolonSeparatedUrls()
    {
        var ports = AdminOptions.ParseListenPorts("http://127.0.0.1:5081;https://127.0.0.1:5082");
        Assert.Equal(2, ports.Count);
        Assert.Contains(5081, ports);
        Assert.Contains(5082, ports);
    }

    [Fact]
    public void ParseListenPorts_IgnoresInvalidSegments()
    {
        var ports = AdminOptions.ParseListenPorts("not-a-url;http://127.0.0.1:5090");
        Assert.Single(ports);
        Assert.Contains(5090, ports);
    }

    [Fact]
    public void ParseListenPorts_DeduplicatesPorts()
    {
        var ports = AdminOptions.ParseListenPorts("http://127.0.0.1:5081;http://localhost:5081");
        Assert.Single(ports);
        Assert.Contains(5081, ports);
    }
}
