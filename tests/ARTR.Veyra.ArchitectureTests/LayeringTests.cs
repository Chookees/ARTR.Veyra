using NetArchTest.Rules;
using Xunit;

namespace ARTR.Veyra.ArchitectureTests;

public sealed class LayeringTests
{
    [Fact]
    public void Core_DoesNotReference_Yarp()
    {
        var result = Types.InAssembly(typeof(ARTR.Veyra.Core.Hosting.VeyraConstants).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Yarp.ReverseProxy")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Core_DoesNotReference_AspNetCore_Hosting()
    {
        var result = Types.InAssembly(typeof(ARTR.Veyra.Core.Hosting.VeyraConstants).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Hosting")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_DoesNotReference_Host()
    {
        var result = Types.InAssembly(typeof(ARTR.Veyra.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("ARTR.Veyra.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Authentication_DoesNotReference_Host()
    {
        var result = Types.InAssembly(typeof(ARTR.Veyra.Authentication.DependencyInjection.AuthenticationServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("ARTR.Veyra.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Observability_DoesNotReference_Host()
    {
        var result = Types.InAssembly(typeof(ARTR.Veyra.Observability.DependencyInjection.ObservabilityServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("ARTR.Veyra.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void HostMayReferenceAllLayers()
    {
        var referenced = typeof(ARTR.Veyra.Host.Admin.AdminEndpointRouteBuilderExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ARTR.Veyra.Core", referenced);
        Assert.Contains("ARTR.Veyra.Infrastructure", referenced);
        Assert.Contains("ARTR.Veyra.Authentication", referenced);
        Assert.Contains("ARTR.Veyra.Observability", referenced);
    }
}
