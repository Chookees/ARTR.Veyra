namespace ARTR.Veyra.Core.Routing;

/// <summary>
/// Well-known YARP route metadata keys used by Veyra authorization and rate limiting.
/// </summary>
public static class VeyraRouteMetadata
{
    public const string AuthorizationPolicy = "AuthorizationPolicy";

    public const string AllowAnonymous = "AllowAnonymous";

    public const string RateLimitPolicy = "RateLimitPolicy";
}
