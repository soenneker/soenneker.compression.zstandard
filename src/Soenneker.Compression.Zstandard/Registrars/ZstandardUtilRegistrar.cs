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
    /// Registers Zstandard Util with a singleton lifetime.
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddZstandardUtilAsSingleton(this IServiceCollection services)
    {
        services.AddFileUtilAsSingleton().TryAddSingleton<IZstandardUtil, ZstandardUtil>();
        return services;
    }

    /// <summary>
    /// Registers Zstandard Util with a scoped lifetime.
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddZstandardUtilAsScoped(this IServiceCollection services)
    {
        services.AddFileUtilAsScoped().TryAddScoped<IZstandardUtil, ZstandardUtil>();
        return services;
    }
}
