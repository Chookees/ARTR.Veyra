using ARTR.Veyra.Authentication.ApiKey;
using ARTR.Veyra.Authentication.Authorization;
using ARTR.Veyra.Authentication.Jwt;
using ARTR.Veyra.Core.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Authentication.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public const string PolicySchemeName = "Veyra";

    public static IServiceCollection AddVeyraAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication(static options =>
            {
                options.DefaultAuthenticateScheme = PolicySchemeName;
                options.DefaultChallengeScheme = PolicySchemeName;
            })
            .AddVeyraApiKey()
            .AddVeyraJwt()
            .AddPolicyScheme(PolicySchemeName, PolicySchemeName, static options =>
            {
                options.ForwardDefaultSelector = SelectAuthenticationScheme;
                // Keep challenge/forbid on the same selected scheme so JWT OnChallenge runs.
                options.ForwardChallenge = null;
                options.ForwardForbid = null;
            });

        services.AddVeyraAuthorization();

        return services;
    }

    private static string SelectAuthenticationScheme(HttpContext context)
    {
        var veyraOptions = context.RequestServices.GetRequiredService<IOptions<VeyraOptions>>().Value;
        var jwtEnabled = veyraOptions.Authentication.Enabled && veyraOptions.Authentication.Jwt.Enabled;
        var apiKeyEnabled = veyraOptions.Authentication.Enabled && veyraOptions.Authentication.ApiKey.Enabled;

        if (jwtEnabled && apiKeyEnabled)
        {
            var authorizationHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
                authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            var apiKeyHeaderName = veyraOptions.Authentication.ApiKey.HeaderName;
            if (context.Request.Headers.TryGetValue(apiKeyHeaderName, out var apiKeyHeader) &&
                !string.IsNullOrWhiteSpace(apiKeyHeader.ToString()))
            {
                return ApiKeyAuthenticationOptions.SchemeName;
            }

            return JwtBearerDefaults.AuthenticationScheme;
        }

        if (jwtEnabled)
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        if (apiKeyEnabled)
        {
            return ApiKeyAuthenticationOptions.SchemeName;
        }

        return ApiKeyAuthenticationOptions.SchemeName;
    }
}
