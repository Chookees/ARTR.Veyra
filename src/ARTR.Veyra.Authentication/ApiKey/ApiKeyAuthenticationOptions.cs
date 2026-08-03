using Microsoft.AspNetCore.Authentication;

namespace ARTR.Veyra.Authentication.ApiKey;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";
}
