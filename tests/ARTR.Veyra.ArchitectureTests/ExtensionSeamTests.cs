using ARTR.Veyra.Core.Extensions;
using NetArchTest.Rules;
using Xunit;

namespace ARTR.Veyra.ArchitectureTests;

public sealed class ExtensionSeamTests
{
    [Fact]
    public void RequestGate_LivesInCore_WithoutYarp()
    {
        var assembly = typeof(IVeyraRequestGate).Assembly;
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace("ARTR.Veyra.Core.Extensions")
            .ShouldNot()
            .HaveDependencyOn("Yarp.ReverseProxy")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void CoreExtensions_DoNotUseReflectionEmit()
    {
        var types = typeof(IVeyraRequestGate).Assembly.GetTypes()
            .Where(t => t.Namespace == "ARTR.Veyra.Core.Extensions");

        foreach (var type in types)
        {
            var refs = type.GetMethods(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetParameters().Select(p => p.ParameterType.FullName ?? string.Empty));

            Assert.DoesNotContain(refs, name =>
                name.Contains("System.Reflection.Emit", StringComparison.Ordinal));
        }
    }
}
