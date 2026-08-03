using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Authentication.ApiKey;

public sealed class VeyraApiKeyAuthenticationPostConfigureOptions : IPostConfigureOptions<ApiKeyAuthenticationOptions>
{
    private readonly IOptionsMonitor<VeyraOptions> _veyraOptions;

    public VeyraApiKeyAuthenticationPostConfigureOptions(IOptionsMonitor<VeyraOptions> veyraOptions)
    {
        _veyraOptions = veyraOptions ?? throw new ArgumentNullException(nameof(veyraOptions));
    }

    public void PostConfigure(string? name, ApiKeyAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.Equals(name, ApiKeyAuthenticationOptions.SchemeName, StringComparison.Ordinal))
        {
            return;
        }

        options.HeaderName = _veyraOptions.CurrentValue.Authentication.ApiKey.HeaderName;
    }
}
