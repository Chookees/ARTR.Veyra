namespace ARTR.Veyra.Core.Secrets;

public interface ISecretResolver
{
    Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default);
}
