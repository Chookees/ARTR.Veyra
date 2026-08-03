using System.Text;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ARTR.Veyra.Authentication.Jwt;

public static class JwtAuthenticationExtensions
{
    public static AuthenticationBuilder AddVeyraJwt(this AuthenticationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme);
        builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, VeyraJwtBearerPostConfigureOptions>();

        return builder;
    }
}
