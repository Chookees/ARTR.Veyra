using ARTR.Veyra.Core.Secrets;

namespace ARTR.Veyra.Infrastructure.Secrets;

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    private const string Prefix = "env:";

    public Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        cancellationToken.ThrowIfCancellationRequested();

        string variableName;
        if (secretName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            variableName = secretName[Prefix.Length..];
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return Task.FromResult<string?>(null);
            }
        }
        else
        {
            variableName = secretName;
        }

        return Task.FromResult(Environment.GetEnvironmentVariable(variableName));
    }
}
