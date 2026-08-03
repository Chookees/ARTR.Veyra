using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Options;
using AspNetAuthorizationOptions = Microsoft.AspNetCore.Authorization.AuthorizationOptions;

namespace ARTR.Veyra.Authentication.Authorization;

internal sealed class VeyraAuthorizationOptionsConfigurer : IConfigureOptions<AspNetAuthorizationOptions>
{
    private readonly VeyraOptions _veyraOptions;

    public VeyraAuthorizationOptionsConfigurer(IOptions<VeyraOptions> veyraOptions)
    {
        ArgumentNullException.ThrowIfNull(veyraOptions);
        _veyraOptions = veyraOptions.Value;
    }

    public void Configure(AspNetAuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(VeyraAuthorizationExtensions.VeyraAdminPolicyName, policy => policy.RequireAuthenticatedUser());

        if (!_veyraOptions.Authorization.Enabled)
        {
            return;
        }

        foreach (var (policyName, roles) in _veyraOptions.Authorization.Policies)
        {
            options.AddPolicy(policyName, policy => policy.RequireRole(roles));
        }
    }
}
