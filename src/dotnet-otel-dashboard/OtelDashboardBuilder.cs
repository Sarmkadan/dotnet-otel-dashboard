namespace DotnetOtelDashboard;

/// <summary>
/// Composition root for the dashboard. Consumers normally use the ASP.NET Core extension methods
/// (<c>AddOtelDashboard</c> / <c>MapOtelDashboard</c>) provided by the hosting package; this type is the
/// framework-agnostic core that those methods wire up.
/// </summary>
public sealed class OtelDashboardBuilder
{
    private readonly OtelDashboardOptions _options = new();
    private Func<OtelDashboardOptions, ITelemetryStore>? _storeFactory;
    private Func<OtelDashboardOptions, ITelemetryStore, ITelemetryReceiver>? _receiverFactory;

    /// <summary>
    /// Applies configuration to the options. Can be called multiple times; later calls win.
    /// </summary>
    /// <param name="configure">Callback receiving the mutable options.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public OtelDashboardBuilder Configure(Action<OtelDashboardOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    /// <summary>
    /// Replaces the default <see cref="InMemoryTelemetryStore"/> with a custom store.
    /// </summary>
    /// <param name="factory">Factory receiving the validated options.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public OtelDashboardBuilder UseStore(Func<OtelDashboardOptions, ITelemetryStore> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _storeFactory = factory;
        return this;
    }

    /// <summary>
    /// Replaces the default OTLP receiver with a custom one. The factory receives the options and the
    /// store that the receiver must write into.
    /// </summary>
    /// <param name="factory">Factory receiving the validated options and the configured store.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public OtelDashboardBuilder UseReceiver(Func<OtelDashboardOptions, ITelemetryStore, ITelemetryReceiver> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _receiverFactory = factory;
        return this;
    }

    /// <summary>
    /// Validates options and creates the store and receiver.
    /// </summary>
    /// <returns>A ready-to-use dashboard instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Options are invalid.</exception>
    /// <exception cref="InvalidOperationException">A factory returned <see langword="null"/>, or no receiver is configured.</exception>
    public OtelDashboard Build()
    {
        _options.Validate();

        ITelemetryStore? store = _storeFactory is null
            ? new InMemoryTelemetryStore(_options)
            : _storeFactory(_options);

        if (store is null)
        {
            throw new InvalidOperationException("The store factory returned null.");
        }

        if (_receiverFactory is null)
        {
            throw new InvalidOperationException("No receiver configured; call UseReceiver.");
        }

        ITelemetryReceiver? receiver = _receiverFactory(_options, store);

        if (receiver is null)
        {
            throw new InvalidOperationException("The receiver factory returned null.");
        }

        return new OtelDashboard(_options, store, receiver);
    }
}

/// <summary>
/// A built dashboard: the store, the receiver and the effective options.
/// </summary>
/// <param name="Options">Validated, immutable-after-build options.</param>
/// <param name="Store">The telemetry store.</param>
/// <param name="Receiver">The OTLP receiver writing into <paramref name="Store"/>.</param>
public sealed record OtelDashboard(OtelDashboardOptions Options, ITelemetryStore Store, ITelemetryReceiver Receiver);
