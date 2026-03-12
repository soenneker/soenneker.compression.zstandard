using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.Compression.Zstandard.Registrars;

public static class ZstandardUtilRegistrar
{
    public static IServiceCollection AddZstandardUtilAsSingleton(this IServiceCollection services)
    {
        services.AddFileUtilAsSingleton().TryAddSingleton<IZstandardUtil, ZstandardUtil>();
        return services;
    }

    public static IServiceCollection AddZstandardUtilAsScoped(this IServiceCollection services)
    {
        services.AddFileUtilAsScoped().TryAddScoped<IZstandardUtil, ZstandardUtil>();
        return services;
    }
}
