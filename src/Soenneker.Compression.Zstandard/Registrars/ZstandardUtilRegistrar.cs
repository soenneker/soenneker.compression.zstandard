using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Compression.Zstandard.Abstract;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.Compression.Zstandard.Registrars;

/// <summary>
/// Represents the zstandard util registrar.
/// </summary>
public static class ZstandardUtilRegistrar
{
    /// <summary>
    /// Adds zstandard util as singleton.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddZstandardUtilAsSingleton(this IServiceCollection services)
    {
        services.AddFileUtilAsSingleton().TryAddSingleton<IZstandardUtil, ZstandardUtil>();
        return services;
    }

    /// <summary>
    /// Adds zstandard util as scoped.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddZstandardUtilAsScoped(this IServiceCollection services)
    {
        services.AddFileUtilAsScoped().TryAddScoped<IZstandardUtil, ZstandardUtil>();
        return services;
    }
}
