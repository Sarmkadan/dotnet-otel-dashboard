using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DotnetOtelDashboard;

/// <summary>
/// Dependency injection registration for the dashboard core services.
/// </summary>
public static class OtelDashboardServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OtelDashboardOptions"/>, <see cref="ITelemetryStore"/> and <see cref="ITelemetryReceiver"/>.
    /// Registrations use <c>TryAdd</c>, so a custom <see cref="ITelemetryStore"/> registered before this call is kept.
    /// </summary>
    /// <param name="services">The service collection to add the dashboard services to.</param>
    /// <param name="configure">Optional callback that configures <see cref="OtelDashboardOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddOtelDashboard(this IServiceCollection services, Action<OtelDashboardOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<OtelDashboardOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OtelDashboardOptions>, OtelDashboardOptionsValidator>());
        optionsBuilder.ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.TryAddSingleton<ITelemetryStore>(sp => new InMemoryTelemetryStore(
            sp.GetRequiredService<IOptions<OtelDashboardOptions>>().Value,
            sp.GetRequiredService<TimeProvider>()));

        services.TryAddSingleton<ITelemetryReceiver>(sp => new OtlpTelemetryReceiver(
            sp.GetRequiredService<ITelemetryStore>()));

        return services;
    }
}
