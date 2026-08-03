using System.Security.Claims;
using System.Text.Encodings.Web;
using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Authentication.ApiKey;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IOptionsMonitor<VeyraOptions> _veyraOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<VeyraOptions> veyraOptions)
        : base(options, logger, encoder)
    {
        _veyraOptions = veyraOptions ?? throw new ArgumentNullException(nameof(veyraOptions));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKeyOptions = _veyraOptions.CurrentValue.Authentication.ApiKey;
        var headerName = string.IsNullOrWhiteSpace(Options.HeaderName)
            ? apiKeyOptions.HeaderName
            : Options.HeaderName;

        if (!Request.Headers.TryGetValue(headerName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presented = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(presented))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key header was empty."));
        }

        string presentedHash;
        try
        {
            presentedHash = ApiKeyHasher.HashSha256Hex(presented);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(AuthenticateResult.Fail("API key was invalid."));
        }

        ApiKeyEntry? match = null;
        foreach (var key in apiKeyOptions.Keys)
        {
            if (ApiKeyHasher.FixedTimeEqualsHex(presentedHash, key.HashSha256Hex))
            {
                match = key;
                break;
            }
        }

        if (match is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("API key was not recognized."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, match.Id),
            new(ClaimTypes.Name, match.Name),
            new("veyra_api_key_id", match.Id),
        };

        foreach (var role in match.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
