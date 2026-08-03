using ARTR.Veyra.Core.Secrets;

namespace ARTR.Veyra.Infrastructure.Secrets;

public sealed class CompositeSecretResolver : ISecretResolver
{
    private readonly ISecretResolver[] _resolvers;

    public CompositeSecretResolver(IEnumerable<ISecretResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        _resolvers = resolvers.ToArray();
        if (_resolvers.Length == 0)
        {
            throw new ArgumentException("At least one secret resolver is required.", nameof(resolvers));
        }
    }

    public async Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        foreach (var resolver in _resolvers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await resolver.ResolveAsync(secretName, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }
}
