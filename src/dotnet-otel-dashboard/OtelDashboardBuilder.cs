namespace DotnetOtelDashboard;

/// <summary>
/// Composition root for the dashboard. Consumers normally use the ASP.NET Core extension methods
/// (<c>AddOtelDashboard</c> / <c>MapOtelDashboard</c>) provided by the hosting package; this type is the
/// framework-agnostic core that those methods wire up.
/// </summary>
public sealed class OtelDashboardBuilder
{
    /// <summary>
    /// Applies configuration to the options. Can be called multiple times; later calls win.
    /// </summary>
    /// <param name="configure">Callback receiving the mutable options.</param>
    /// <returns>This builder.</returns>
    public OtelDashboardBuilder Configure(Action<OtelDashboardOptions> configure) => throw new NotImplementedException();

    /// <summary>
    /// Replaces the default <see cref="InMemoryTelemetryStore"/> with a custom store.
    /// </summary>
    /// <param name="factory">Factory receiving the validated options.</param>
    /// <returns>This builder.</returns>
    public OtelDashboardBuilder UseStore(Func<OtelDashboardOptions, ITelemetryStore> factory) => throw new NotImplementedException();

    /// <summary>
    /// Validates options and creates the store and receiver.
    /// </summary>
    /// <returns>A ready-to-use dashboard instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Options are invalid.</exception>
    public OtelDashboard Build() => throw new NotImplementedException();
}

/// <summary>
/// A built dashboard: the store, the receiver and the effective options.
/// </summary>
/// <param name="Options">Validated, immutable-after-build options.</param>
/// <param name="Store">The telemetry store.</param>
/// <param name="Receiver">The OTLP receiver writing into <paramref name="Store"/>.</param>
public sealed record OtelDashboard(OtelDashboardOptions Options, ITelemetryStore Store, ITelemetryReceiver Receiver);
