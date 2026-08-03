using System.Net;
using System.Text.RegularExpressions;
using ARTR.Veyra.Core.Transforms;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Core.Configuration;

public sealed partial class VeyraOptionsValidator : IValidateOptions<VeyraOptions>
{
    private static readonly Regex Sha256HexPattern = Sha256HexRegex();

    public ValidateOptionsResult Validate(string? name, VeyraOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateAdmin(options.Admin, failures);
        ValidateAuthentication(options.Authentication, failures);
        ValidateAuthorization(options.Authorization, failures);
        ValidateRateLimiting(options.RateLimiting, failures);
        ValidateRequestLimits(options.RequestLimits, failures);
        ValidateForwardedHeaders(options.ForwardedHeaders, failures);
        ValidateObservability(options.Observability, failures);
        ValidateSecrets(options.Secrets, failures);
        ValidateHealth(options.Health, failures);
        ValidateTransforms(options.Transforms, failures);
        ValidateShutdown(options.Shutdown, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateAdmin(AdminOptions admin, List<string> failures)
    {
        if (!IsAbsolutePath(admin.PathBase))
        {
            failures.Add($"Admin.PathBase must be an absolute path starting with '/'. Actual: '{admin.PathBase}'.");
        }

        if (string.Equals(admin.PathBase, "/", StringComparison.Ordinal))
        {
            failures.Add("Admin.PathBase cannot be the root path '/'.");
        }

        if (!string.IsNullOrWhiteSpace(admin.ListenUrls))
        {
            foreach (var part in admin.ListenUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Uri.TryCreate(part, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    failures.Add($"Admin.ListenUrls contains an invalid URL '{part}'.");
                }
            }
        }
    }

    private static void ValidateAuthentication(AuthenticationOptions authentication, List<string> failures)
    {
        if (!authentication.Enabled)
        {
            return;
        }

        if (!authentication.Jwt.Enabled && !authentication.ApiKey.Enabled)
        {
            failures.Add("Authentication.Enabled requires at least one of Jwt.Enabled or ApiKey.Enabled.");
        }

        ValidateJwt(authentication.Jwt, failures);
        ValidateApiKey(authentication.ApiKey, failures);
    }

    private static void ValidateJwt(JwtOptions jwt, List<string> failures)
    {
        if (!jwt.Enabled)
        {
            return;
        }

        var hasAuthority = !string.IsNullOrWhiteSpace(jwt.Authority);
        var hasSigningKeySecret = !string.IsNullOrWhiteSpace(jwt.SigningKeySecretName);

        if (!hasAuthority && !hasSigningKeySecret)
        {
            failures.Add("Authentication.Jwt.Enabled requires Authority or SigningKeySecretName.");
        }

        if (hasAuthority && !Uri.TryCreate(jwt.Authority, UriKind.Absolute, out _))
        {
            failures.Add($"Authentication.Jwt.Authority must be an absolute URI. Actual: '{jwt.Authority}'.");
        }

        if (!string.IsNullOrWhiteSpace(jwt.MetadataAddress) &&
            !Uri.TryCreate(jwt.MetadataAddress, UriKind.Absolute, out _))
        {
            failures.Add(
                $"Authentication.Jwt.MetadataAddress must be an absolute URI. Actual: '{jwt.MetadataAddress}'.");
        }
    }

    private static void ValidateApiKey(ApiKeyOptions apiKey, List<string> failures)
    {
        if (!apiKey.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey.HeaderName))
        {
            failures.Add("Authentication.ApiKey.HeaderName is required when ApiKey is enabled.");
        }

        if (apiKey.Keys.Count == 0)
        {
            failures.Add("Authentication.ApiKey.Keys must contain at least one entry when ApiKey is enabled.");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < apiKey.Keys.Count; index++)
        {
            var entry = apiKey.Keys[index];
            var prefix = $"Authentication.ApiKey.Keys[{index}]";

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                failures.Add($"{prefix}.Id is required.");
            }
            else if (!seenIds.Add(entry.Id))
            {
                failures.Add($"{prefix}.Id '{entry.Id}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                failures.Add($"{prefix}.Name is required.");
            }

            if (string.IsNullOrWhiteSpace(entry.HashSha256Hex))
            {
                failures.Add($"{prefix}.HashSha256Hex is required.");
            }
            else if (!Sha256HexPattern.IsMatch(entry.HashSha256Hex))
            {
                failures.Add($"{prefix}.HashSha256Hex must be a 64-character lowercase hexadecimal SHA-256 hash.");
            }

            if (entry.Roles.Any(static role => string.IsNullOrWhiteSpace(role)))
            {
                failures.Add($"{prefix}.Roles cannot contain empty values.");
            }
        }
    }

    private static void ValidateAuthorization(AuthorizationOptions authorization, List<string> failures)
    {
        if (!authorization.Enabled)
        {
            return;
        }

        if (authorization.Policies.Count == 0)
        {
            failures.Add("Authorization.Enabled requires at least one policy.");
            return;
        }

        foreach (var (policyName, roles) in authorization.Policies)
        {
            if (string.IsNullOrWhiteSpace(policyName))
            {
                failures.Add("Authorization.Policies contains an empty policy name.");
                continue;
            }

            if (roles is null || roles.Length == 0)
            {
                failures.Add($"Authorization.Policies['{policyName}'] must contain at least one role.");
                continue;
            }

            if (roles.Any(static role => string.IsNullOrWhiteSpace(role)))
            {
                failures.Add($"Authorization.Policies['{policyName}'] cannot contain empty role values.");
            }
        }
    }

    private static void ValidateRateLimiting(RateLimitingOptions rateLimiting, List<string> failures)
    {
        if (!rateLimiting.Enabled)
        {
            return;
        }

        if (rateLimiting.GlobalPermitLimit <= 0)
        {
            failures.Add("RateLimiting.GlobalPermitLimit must be greater than zero when rate limiting is enabled.");
        }

        if (rateLimiting.GlobalWindowSeconds <= 0)
        {
            failures.Add("RateLimiting.GlobalWindowSeconds must be greater than zero when rate limiting is enabled.");
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rateLimiting.Policies.Count; index++)
        {
            var policy = rateLimiting.Policies[index];
            var prefix = $"RateLimiting.Policies[{index}]";

            if (string.IsNullOrWhiteSpace(policy.Name))
            {
                failures.Add($"{prefix}.Name is required.");
            }
            else if (!seenNames.Add(policy.Name))
            {
                failures.Add($"{prefix}.Name '{policy.Name}' is duplicated.");
            }

            if (policy.PermitLimit <= 0)
            {
                failures.Add($"{prefix}.PermitLimit must be greater than zero.");
            }

            if (policy.WindowSeconds <= 0)
            {
                failures.Add($"{prefix}.WindowSeconds must be greater than zero.");
            }

            if (policy.QueueLimit < 0)
            {
                failures.Add($"{prefix}.QueueLimit cannot be negative.");
            }
        }
    }

    private static void ValidateRequestLimits(RequestLimitsOptions requestLimits, List<string> failures)
    {
        if (requestLimits.MaxRequestBodyBytes <= 0)
        {
            failures.Add("RequestLimits.MaxRequestBodyBytes must be greater than zero.");
        }

        if (requestLimits.RequestHeadersTimeoutSeconds <= 0)
        {
            failures.Add("RequestLimits.RequestHeadersTimeoutSeconds must be greater than zero.");
        }

        if (requestLimits.KeepAliveTimeoutSeconds <= 0)
        {
            failures.Add("RequestLimits.KeepAliveTimeoutSeconds must be greater than zero.");
        }
    }

    private static void ValidateForwardedHeaders(ForwardedHeadersOptions forwardedHeaders, List<string> failures)
    {
        if (!forwardedHeaders.Enabled)
        {
            return;
        }

        if (forwardedHeaders.ForwardLimit is < 0)
        {
            failures.Add("ForwardedHeaders.ForwardLimit cannot be negative.");
        }

        foreach (var proxy in forwardedHeaders.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add($"ForwardedHeaders.KnownProxies contains an invalid IP address: '{proxy}'.");
            }
        }

        foreach (var network in forwardedHeaders.KnownNetworks)
        {
            if (!TryParseCidr(network, out _))
            {
                failures.Add($"ForwardedHeaders.KnownNetworks contains an invalid CIDR network: '{network}'.");
            }
        }
    }

    private static void ValidateObservability(ObservabilityOptions observability, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(observability.ServiceName))
        {
            failures.Add("Observability.ServiceName is required.");
        }

        if (observability.Otlp.Enabled && string.IsNullOrWhiteSpace(observability.Otlp.Endpoint))
        {
            failures.Add("Observability.Otlp.Endpoint is required when OTLP export is enabled.");
        }

        if (observability.Otlp.Enabled &&
            !Uri.TryCreate(observability.Otlp.Endpoint, UriKind.Absolute, out _))
        {
            failures.Add(
                $"Observability.Otlp.Endpoint must be an absolute URI. Actual: '{observability.Otlp.Endpoint}'.");
        }

        if (observability.Prometheus.Enabled && !IsAbsolutePath(observability.Prometheus.Path))
        {
            failures.Add(
                $"Observability.Prometheus.Path must be an absolute path starting with '/'. Actual: '{observability.Prometheus.Path}'.");
        }
    }

    private static void ValidateSecrets(SecretsOptions secrets, List<string> failures)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < secrets.Providers.Count; index++)
        {
            var provider = secrets.Providers[index];
            var prefix = $"Secrets.Providers[{index}]";

            if (string.IsNullOrWhiteSpace(provider.Type))
            {
                failures.Add($"{prefix}.Type is required.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(provider.Name) && !seenNames.Add(provider.Name))
            {
                failures.Add($"{prefix}.Name '{provider.Name}' is duplicated.");
            }

            if (string.Equals(provider.Type, "File", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(provider.Path))
            {
                failures.Add($"{prefix}.Path is required for File secret providers.");
            }
        }
    }

    private static void ValidateHealth(HealthOptions health, List<string> failures)
    {
        if (!health.Enabled)
        {
            return;
        }

        if (!IsAbsolutePath(health.LivePath))
        {
            failures.Add($"Health.LivePath must be an absolute path starting with '/'. Actual: '{health.LivePath}'.");
        }

        if (!IsAbsolutePath(health.ReadyPath))
        {
            failures.Add($"Health.ReadyPath must be an absolute path starting with '/'. Actual: '{health.ReadyPath}'.");
        }

        if (!IsAbsolutePath(health.StartupPath))
        {
            failures.Add(
                $"Health.StartupPath must be an absolute path starting with '/'. Actual: '{health.StartupPath}'.");
        }
    }

    private static void ValidateTransforms(TransformsOptions transforms, List<string> failures)
    {
        if (transforms.Allowlist.Count == 0)
        {
            return;
        }

        if (transforms.Allowlist.Any(static key => string.IsNullOrWhiteSpace(key)))
        {
            failures.Add("Transforms.Allowlist cannot contain empty values.");
        }

        var duplicates = transforms.Allowlist
            .GroupBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            failures.Add(
                $"Transforms.Allowlist contains duplicate entries: {string.Join(", ", duplicates)}.");
        }

        var unknownKeys = transforms.Allowlist
            .Where(static key => !TransformAllowlist.DefaultAllowlist.Contains(key))
            .ToArray();

        if (unknownKeys.Length > 0)
        {
            failures.Add(
                $"Transforms.Allowlist contains keys not present in the default allowlist: {string.Join(", ", unknownKeys)}.");
        }
    }

    private static void ValidateShutdown(ShutdownOptions shutdown, List<string> failures)
    {
        if (shutdown.ShutdownTimeoutSeconds <= 0)
        {
            failures.Add("Shutdown.ShutdownTimeoutSeconds must be greater than zero.");
        }
    }

    private static bool IsAbsolutePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/');

    private static bool TryParseCidr(string value, out (IPAddress Address, int PrefixLength) cidr)
    {
        cidr = default;
        var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        cidr = (address, prefixLength);
        return true;
    }

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256HexRegex();
}
