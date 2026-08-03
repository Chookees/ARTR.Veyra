using System.Diagnostics;
using System.Text;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Errors;
using ARTR.Veyra.Core.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ARTR.Veyra.Authentication.Jwt;

public sealed class VeyraJwtBearerPostConfigureOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly IOptionsMonitor<VeyraOptions> _veyraOptions;
    private readonly ISecretResolver _secretResolver;

    public VeyraJwtBearerPostConfigureOptions(
        IOptionsMonitor<VeyraOptions> veyraOptions,
        ISecretResolver secretResolver)
    {
        _veyraOptions = veyraOptions ?? throw new ArgumentNullException(nameof(veyraOptions));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var jwtOptions = _veyraOptions.CurrentValue.Authentication.Jwt;
        if (!_veyraOptions.CurrentValue.Authentication.Enabled || !jwtOptions.Enabled)
        {
            return;
        }

        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
        options.Audience = jwtOptions.Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtOptions.Audience),
            ValidAudience = jwtOptions.Audience,
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer) || !string.IsNullOrWhiteSpace(jwtOptions.Authority),
            ValidIssuer = jwtOptions.Issuer,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            TryAllIssuerSigningKeys = true,
        };

        if (!string.IsNullOrWhiteSpace(jwtOptions.SigningKeySecretName))
        {
            ConfigureSymmetricSigningKey(options, jwtOptions);
            return;
        }

        if (!string.IsNullOrWhiteSpace(jwtOptions.Authority))
        {
            options.Authority = jwtOptions.Authority;
        }

        if (!string.IsNullOrWhiteSpace(jwtOptions.MetadataAddress))
        {
            options.MetadataAddress = jwtOptions.MetadataAddress;
        }

        AttachChallengeEvents(options);
    }

    private void ConfigureSymmetricSigningKey(JwtBearerOptions options, JwtOptions jwtOptions)
    {
        var signingKey = _secretResolver
            .ResolveAsync(jwtOptions.SigningKeySecretName!)
            .GetAwaiter()
            .GetResult();

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new SecretResolutionException(
                $"JWT signing key secret '{jwtOptions.SigningKeySecretName}' could not be resolved.");
        }

        // Local symmetric validation only — never consult OIDC metadata.
        options.Authority = null;
        options.MetadataAddress = null!;
        options.RequireHttpsMetadata = false;
        options.RefreshOnIssuerKeyNotFound = false;
        options.Configuration = new OpenIdConnectConfiguration();
        options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.SignatureValidator = null;
        AttachChallengeEvents(options);
    }

    private static void AttachChallengeEvents(JwtBearerOptions options)
    {
        var priorFailed = options.Events?.OnAuthenticationFailed;
        var priorChallenge = options.Events?.OnChallenge;
        options.Events ??= new JwtBearerEvents();

        options.Events.OnAuthenticationFailed = async context =>
        {
            // Swallow token parse/validation exceptions so they become auth failures (401), not 500s.
            if (priorFailed is not null)
            {
                await priorFailed(context).ConfigureAwait(false);
            }
        };

        options.Events.OnChallenge = async context =>
        {
            if (priorChallenge is not null)
            {
                await priorChallenge(context).ConfigureAwait(false);
                if (context.Handled)
                {
                    return;
                }
            }

            context.HandleResponse();
            if (context.Response.HasStarted)
            {
                return;
            }

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Authentication is required or the provided credentials are invalid.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["errorCode"] = VeyraErrorCodes.AuthInvalid,
                    ["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier,
                },
            };

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json").ConfigureAwait(false);
        };
    }
}
