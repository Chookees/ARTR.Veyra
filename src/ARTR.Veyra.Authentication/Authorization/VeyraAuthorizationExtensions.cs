using ARTR.Veyra.Core.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Authentication.Authorization;

public static class VeyraAuthorizationExtensions
{
    public const string VeyraAdminPolicyName = "VeyraAdmin";

    public static IServiceCollection AddVeyraAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization();
        services.AddSingleton<IConfigureOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>, VeyraAuthorizationOptionsConfigurer>();

        return services;
    }
}
