using ARTR.Veyra.Core.Plugins;
using ARTR.Veyra.Infrastructure.Plugins;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class PluginLoaderTests
{
    [Fact]
    public void Load_RejectsPathOutsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "veyra-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // Temp roots are rejected as insecure — use a non-temp path under the test output.
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var allowed = Path.Combine(AppContext.BaseDirectory, "plugin-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(allowed);
        try
        {
            var outside = Path.Combine(AppContext.BaseDirectory, "outside-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(outside, [1, 2, 3]);
            var loader = new Sha256PluginLoader();
            var results = loader.Load(
                [new PluginDescriptor { Id = "p1", Path = outside, Sha256Hex = new string('a', 64) }],
                allowed);
            Assert.Contains(results, r => !r.Success && r.Error!.Contains("AllowedRoot", StringComparison.Ordinal));
            File.Delete(outside);
        }
        finally
        {
            Directory.Delete(allowed, recursive: true);
        }
    }

    [Fact]
    public void Load_AcceptsMatchingHash()
    {
        var allowed = Path.Combine(AppContext.BaseDirectory, "plugin-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(allowed);
        try
        {
            var path = Path.Combine(allowed, "plugin.bin");
            var bytes = "veyra-plugin"u8.ToArray();
            File.WriteAllBytes(path, bytes);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

            var loader = new Sha256PluginLoader();
            var results = loader.Load(
                [new PluginDescriptor { Id = "p1", Path = path, Sha256Hex = hash }],
                allowed);

            Assert.True(results.Single().Success);
        }
        finally
        {
            Directory.Delete(allowed, recursive: true);
        }
    }

    [Fact]
    public void Load_RejectsTempAllowedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "veyra-plugin-insecure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var loader = new Sha256PluginLoader();
            var results = loader.Load(
                [new PluginDescriptor { Id = "p1", Path = Path.Combine(root, "x.bin"), Sha256Hex = new string('a', 64) }],
                root);
            Assert.Contains(results, r => !r.Success && r.Error!.Contains("temporary", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
