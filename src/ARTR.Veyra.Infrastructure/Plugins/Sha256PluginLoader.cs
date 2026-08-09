using System.Security.Cryptography;
using ARTR.Veyra.Core.Plugins;

namespace ARTR.Veyra.Infrastructure.Plugins;

/// <summary>
/// Opt-in plugin loader that verifies SHA-256 and path containment under AllowedRoot.
/// Validates packaging integrity only; does not execute arbitrary plugin code.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class Sha256PluginLoader : IPluginLoader
{
    public IReadOnlyList<PluginLoadResult> Load(IEnumerable<PluginDescriptor> plugins, string allowedRoot)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedRoot);

        var rootFull = Path.GetFullPath(allowedRoot);
        if (!Directory.Exists(rootFull))
        {
            return
            [
                new PluginLoadResult
                {
                    PluginId = "*",
                    Success = false,
                    Error = "AllowedRoot directory does not exist.",
                },
            ];
        }

        if (IsInsecureRoot(rootFull))
        {
            return
            [
                new PluginLoadResult
                {
                    PluginId = "*",
                    Success = false,
                    Error = "AllowedRoot must not be a world-writable or temporary directory.",
                },
            ];
        }

        var results = new List<PluginLoadResult>();

        foreach (var plugin in plugins)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plugin.Id) ||
                    string.IsNullOrWhiteSpace(plugin.Path) ||
                    string.IsNullOrWhiteSpace(plugin.Sha256Hex))
                {
                    results.Add(new PluginLoadResult
                    {
                        PluginId = plugin.Id,
                        Success = false,
                        Error = "Plugin id, path, and sha256 are required.",
                    });
                    continue;
                }

                var fullPath = Path.GetFullPath(plugin.Path);
                if (!fullPath.StartsWith(rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(fullPath, rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new PluginLoadResult
                    {
                        PluginId = plugin.Id,
                        Success = false,
                        Error = "Plugin path escapes AllowedRoot.",
                    });
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    results.Add(new PluginLoadResult
                    {
                        PluginId = plugin.Id,
                        Success = false,
                        Error = "Plugin file not found.",
                    });
                    continue;
                }

                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant();
                if (!string.Equals(hash, plugin.Sha256Hex, StringComparison.Ordinal))
                {
                    results.Add(new PluginLoadResult
                    {
                        PluginId = plugin.Id,
                        Success = false,
                        Error = "Plugin SHA-256 mismatch.",
                    });
                    continue;
                }

                results.Add(new PluginLoadResult { PluginId = plugin.Id, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new PluginLoadResult
                {
                    PluginId = plugin.Id,
                    Success = false,
                    Error = ex.GetType().Name,
                });
            }
        }

        return results;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static bool IsInsecureRoot(string rootFull)
    {
        var temp = Path.GetTempPath();
        if (rootFull.StartsWith(Path.GetFullPath(temp), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                var mode = File.GetUnixFileMode(rootFull);
                if (mode.HasFlag(UnixFileMode.OtherWrite))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // If we cannot inspect permissions, fail closed for plugin roots.
                return true;
            }
        }

        return false;
    }
}