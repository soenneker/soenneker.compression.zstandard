using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Compression.Zstandard.Abstract;

namespace Soenneker.Compression.Zstandard.Registrars;

public static class ZstandardUtilRegistrar
{
    public static IServiceCollection AddZstandardUtilAsSingleton(this IServiceCollection services)
    {
        services.TryAddSingleton<IZstandardUtil, ZstandardUtil>();
        return services;
    }

    public static IServiceCollection AddZstandardUtilAsScoped(this IServiceCollection services)
    {
        services.TryAddScoped<IZstandardUtil, ZstandardUtil>();
        return services;
    }
}
