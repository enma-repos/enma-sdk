using System;
using Enma.Sdk.Core;
using Enma.Sdk.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Enma.Sdk.DependencyInjection;

/// <summary>
/// Extension methods for registering Enma SDK services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEnmaClient"/> as a singleton with the provided configuration.
    /// Adds a hosted service for graceful shutdown.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure <see cref="EnmaClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEnma(
        this IServiceCollection services,
        Action<EnmaClientOptions> configure)
    {
        services.Configure(configure);
        return services.AddEnmaCore();
    }

    /// <summary>
    /// Registers <see cref="IEnmaClient"/> as a singleton, binding options from the "Enma" configuration section.
    /// Adds a hosted service for graceful shutdown.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration section to bind (typically the "Enma" section).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEnma(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EnmaClientOptions>(configuration);
        return services.AddEnmaCore();
    }

    private static IServiceCollection AddEnmaCore(this IServiceCollection services)
    {
        services.AddHttpClient<HttpEventTransport>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<EnmaClientOptions>>().Value;
            client.BaseAddress = options.BaseUrl;
        });

        services.AddSingleton<IEnmaClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EnmaClientOptions>>().Value;
            var transport = sp.GetRequiredService<HttpEventTransport>();
            return new EnmaClient(options, transport);
        });

        services.AddSingleton<IHostedService, EnmaBackgroundService>();

        return services;
    }
}
