using ARTR.Veyra.Core.Configuration;
using ARTR.Veyra.Core.Transforms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Infrastructure.Transforms;

public sealed class YarpTransformAllowlistValidator
{
    private readonly IConfiguration _configuration;
    private readonly IOptions<VeyraOptions> _options;

    public YarpTransformAllowlistValidator(IConfiguration configuration, IOptions<VeyraOptions> options)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public TransformAllowlistValidationResult ValidateConfiguredTransforms()
    {
        var allowlist = BuildAllowlist(_options.Value.Transforms.Allowlist);
        var transforms = new List<IReadOnlyDictionary<string, object?>>();
        var routes = _configuration.GetSection("ReverseProxy:Routes").GetChildren();

        foreach (var route in routes)
        {
            var transformSections = route.GetSection("Transforms").GetChildren();
            foreach (var transformSection in transformSections)
            {
                var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in transformSection.GetChildren())
                {
                    map[child.Key] = child.Value;
                }

                if (map.Count > 0)
                {
                    transforms.Add(map);
                }
            }
        }

        return TransformAllowlist.Validate(transforms, allowlist);
    }

    private static IReadOnlySet<string> BuildAllowlist(IList<string> configured)
    {
        if (configured.Count == 0)
        {
            return TransformAllowlist.DefaultAllowlist;
        }

        return new HashSet<string>(configured, StringComparer.OrdinalIgnoreCase);
    }
}
