using ARTR.Veyra.Core.Secrets;
using Microsoft.Extensions.Configuration;

namespace ARTR.Veyra.Infrastructure.Secrets;

public sealed class ConfigurationSecretResolver : ISecretResolver
{
    private readonly IConfiguration _configuration;

    public ConfigurationSecretResolver(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        cancellationToken.ThrowIfCancellationRequested();

        var path = secretName.StartsWith("config:", StringComparison.OrdinalIgnoreCase)
            ? secretName["config:".Length..]
            : secretName;

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(_configuration[path]);
    }
}
