namespace ARTR.Veyra.Core.Configuration;

public sealed class VeyraOptions
{
    public const string SectionName = "ARTR:Veyra";

    public AdminOptions Admin { get; init; } = new();

    public AuthenticationOptions Authentication { get; init; } = new();

    public AuthorizationOptions Authorization { get; init; } = new();

    public RateLimitingOptions RateLimiting { get; init; } = new();

    public RequestLimitsOptions RequestLimits { get; init; } = new();

    public ForwardedHeadersOptions ForwardedHeaders { get; init; } = new();

    public TlsOptions Tls { get; init; } = new();

    public ObservabilityOptions Observability { get; init; } = new();

    public SecretsOptions Secrets { get; init; } = new();

    public HealthOptions Health { get; init; } = new();

    public TransformsOptions Transforms { get; init; } = new();

    public ShutdownOptions Shutdown { get; init; } = new();

    public ConfigurationReloadOptions ConfigurationReload { get; init; } = new();
}

public sealed class AdminOptions
{
    public bool Enabled { get; init; } = true;

    public string PathBase { get; init; } = "/_veyra";

    public bool RequireAuthentication { get; init; } = true;

    /// <summary>
    /// Optional dedicated Kestrel URL(s) for the admin plane (semicolon-separated).
    /// When set, admin endpoints are only served on these listeners and data-plane listeners reject admin paths.
    /// </summary>
    public string? ListenUrls { get; init; }

    public static HashSet<int> ParseListenPorts(string? listenUrls)
    {
        var ports = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(listenUrls))
        {
            return ports;
        }

        foreach (var part in listenUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Uri.TryCreate(part, UriKind.Absolute, out var uri) && uri.Port > 0)
            {
                ports.Add(uri.Port);
            }
        }

        return ports;
    }
}

public sealed class ConfigurationReloadOptions
{
    public bool Enabled { get; init; } = true;

    public bool RetainLastKnownGood { get; init; } = true;
}

public sealed class AuthenticationOptions
{
    public bool Enabled { get; init; }

    public JwtOptions Jwt { get; init; } = new();

    public ApiKeyOptions ApiKey { get; init; } = new();
}

public sealed class JwtOptions
{
    public bool Enabled { get; init; }

    public string? Authority { get; init; }

    public string? Audience { get; init; }

    public string? Issuer { get; init; }

    public string? MetadataAddress { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;

    public string? SigningKeySecretName { get; init; }
}

public sealed class ApiKeyOptions
{
    public bool Enabled { get; init; }

    public string HeaderName { get; init; } = "X-Api-Key";

    public IList<ApiKeyEntry> Keys { get; init; } = [];
}

public sealed class ApiKeyEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string HashSha256Hex { get; init; } = string.Empty;

    public IList<string> Roles { get; init; } = [];
}

public sealed class AuthorizationOptions
{
    public bool Enabled { get; init; }

    public IDictionary<string, string[]> Policies { get; init; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RateLimitingOptions
{
    public bool Enabled { get; init; }

    public int GlobalPermitLimit { get; init; } = 100;

    public int GlobalWindowSeconds { get; init; } = 60;

    public IList<RateLimitPolicyOptions> Policies { get; init; } = [];
}

public sealed class RateLimitPolicyOptions
{
    public string Name { get; init; } = string.Empty;

    public int PermitLimit { get; init; }

    public int WindowSeconds { get; init; }

    public int QueueLimit { get; init; }
}

public sealed class RequestLimitsOptions
{
    public long MaxRequestBodyBytes { get; init; } = 10_485_760;

    public int RequestHeadersTimeoutSeconds { get; init; } = 30;

    public int KeepAliveTimeoutSeconds { get; init; } = 120;
}

public sealed class ForwardedHeadersOptions
{
    public bool Enabled { get; init; }

    public IList<string> KnownProxies { get; init; } = [];

    public IList<string> KnownNetworks { get; init; } = [];

    public int? ForwardLimit { get; init; }
}

public sealed class TlsOptions
{
    public bool UseHttpsRedirection { get; init; }
}

public sealed class ObservabilityOptions
{
    public string ServiceName { get; init; } = "ARTR.Veyra";

    public OtlpExporterOptions Otlp { get; init; } = new();

    public PrometheusExporterOptions Prometheus { get; init; } = new();

    public bool ConsoleLogging { get; init; } = true;
}

public sealed class OtlpExporterOptions
{
    public bool Enabled { get; init; }

    public string? Endpoint { get; init; }
}

public sealed class PrometheusExporterOptions
{
    public bool Enabled { get; init; }

    public string Path { get; init; } = "/metrics";
}

public sealed class SecretsOptions
{
    public IList<SecretProviderOptions> Providers { get; init; } = [];
}

public sealed class SecretProviderOptions
{
    public string Type { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? Path { get; init; }
}

public sealed class HealthOptions
{
    public bool Enabled { get; init; } = true;

    public string LivePath { get; init; } = "/health/live";

    public string ReadyPath { get; init; } = "/health/ready";

    public string StartupPath { get; init; } = "/health/startup";
}

public sealed class TransformsOptions
{
    public IList<string> Allowlist { get; init; } = [];
}

public sealed class ShutdownOptions
{
    public int ShutdownTimeoutSeconds { get; init; } = 30;
}
