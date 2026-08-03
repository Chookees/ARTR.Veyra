using ARTR.Veyra.Core.RateLimiting;
using ARTR.Veyra.Core.Secrets;
using ARTR.Veyra.Infrastructure.Configuration;
using ARTR.Veyra.Infrastructure.RateLimiting;
using ARTR.Veyra.Infrastructure.Secrets;
using ARTR.Veyra.Infrastructure.Transforms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Veyra.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddVeyraInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IRateLimiterStore, MemoryRateLimiterStore>();
        services.AddSingleton<YarpTransformAllowlistValidator>();
        services.AddSingleton<ConfigurationActivationService>();
        services.AddSingleton<IConfigurationActivationState>(sp => sp.GetRequiredService<ConfigurationActivationService>());
        services.AddHostedService(sp => sp.GetRequiredService<ConfigurationActivationService>());
        services.AddSingleton<ISecretResolver>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            ISecretResolver[] resolvers =
            [
                new EnvironmentSecretResolver(),
                new ConfigurationSecretResolver(config),
                new FileSecretResolver(),
            ];
            return new CompositeSecretResolver(resolvers);
        });

        return services;
    }
}
