using ARTR.Veyra.Core.Secrets;

namespace ARTR.Veyra.Infrastructure.Secrets;

public sealed class FileSecretResolver : ISecretResolver
{
    public async Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        if (!secretName.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = secretName["file:".Length..];
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new SecretResolutionException($"Secret file '{path}' was not found.");
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return content.Trim();
    }
}
