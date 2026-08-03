using ARTR.Veyra.Core.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Authentication.ApiKey;

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddVeyraApiKey(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IPostConfigureOptions<ApiKeyAuthenticationOptions>, VeyraApiKeyAuthenticationPostConfigureOptions>();

        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.SchemeName,
            static _ => { });
    }
}
