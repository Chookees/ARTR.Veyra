using ARTR.Veyra.Infrastructure.Transforms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ARTR.Veyra.UnitTests;

public sealed class YarpTransformAllowlistValidatorTests
{
    [Fact]
    public void ValidateConfiguredTransformsSucceedsForAllowedTransforms()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Routes:route-a:Transforms:0:PathPattern"] = "/{**catch-all}",
            })
            .Build();

        var options = Options.Create(new ARTR.Veyra.Core.Configuration.VeyraOptions());
        var validator = new YarpTransformAllowlistValidator(configuration, options);

        var result = validator.ValidateConfiguredTransforms();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateConfiguredTransformsFailsForDisallowedTransform()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Routes:route-a:Transforms:0:EvilTransform"] = "value",
            })
            .Build();

        var options = Options.Create(new ARTR.Veyra.Core.Configuration.VeyraOptions());
        var validator = new YarpTransformAllowlistValidator(configuration, options);

        var result = validator.ValidateConfiguredTransforms();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("EvilTransform", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateConfiguredTransformsUsesCustomAllowlistWhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Routes:route-a:Transforms:0:PathPrefix"] = "/api",
            })
            .Build();

        var options = Options.Create(new ARTR.Veyra.Core.Configuration.VeyraOptions
        {
            Transforms = new ARTR.Veyra.Core.Configuration.TransformsOptions
            {
                Allowlist = ["PathPrefix"],
            },
        });
        var validator = new YarpTransformAllowlistValidator(configuration, options);

        var result = validator.ValidateConfiguredTransforms();

        Assert.True(result.IsValid);
    }
}
