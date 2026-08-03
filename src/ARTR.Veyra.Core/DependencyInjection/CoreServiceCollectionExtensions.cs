using ARTR.Veyra.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ARTR.Veyra.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddVeyraCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<VeyraOptions>()
            .Bind(configuration.GetSection(VeyraOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<VeyraOptions>, VeyraOptionsValidator>();

        return services;
    }
}
