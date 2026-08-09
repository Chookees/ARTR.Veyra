namespace ARTR.Veyra.Core.Plugins;

/// <summary>
/// Describes an opt-in signed plugin assembly. Loading is disabled unless Features.Plugins.Enabled is true.
/// </summary>
public sealed class PluginDescriptor
{
    public string Id { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the assembly file bytes.</summary>
    public string Sha256Hex { get; init; } = string.Empty;
}

public interface IPluginLoader
{
    IReadOnlyList<PluginLoadResult> Load(IEnumerable<PluginDescriptor> plugins, string allowedRoot);
}

public sealed class PluginLoadResult
{
    public required string PluginId { get; init; }

    public bool Success { get; init; }

    public string? Error { get; init; }
}
