namespace ARTR.Veyra.Core.Secrets;

public sealed class SecretResolutionException : Exception
{
    public SecretResolutionException()
    {
        SecretName = string.Empty;
    }

    public SecretResolutionException(string message)
        : base(message)
    {
        SecretName = string.Empty;
    }

    public SecretResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
        SecretName = string.Empty;
    }

    public SecretResolutionException(string secretName, string message)
        : base(message)
    {
        SecretName = secretName;
    }

    public SecretResolutionException(string secretName, string message, Exception innerException)
        : base(message, innerException)
    {
        SecretName = secretName;
    }

    public string SecretName { get; }
}
