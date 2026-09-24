namespace DotnetOtelDashboard;

/// <summary>
/// Default <see cref="ITelemetryStore"/>: bounded in-memory ring buffers per signal, evicting by count and by
/// <see cref="OtelDashboardOptions.Retention"/>.
/// </summary>
public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    private readonly OtelDashboardOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a store bounded by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Limits and retention.</param>
    /// <param name="timeProvider">Clock used for retention eviction; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryTelemetryStore(OtelDashboardOptions options, TimeProvider? timeProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask WriteSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask WriteMetricsAsync(IReadOnlyList<MetricPoint> points, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask WriteLogsAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpanRecord>> QuerySpansAsync(SpanQuery query, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpanRecord>> GetTraceAsync(string traceId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<MetricPoint>> QueryMetricsAsync(MetricQuery query, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LogRecord>> QueryLogsAsync(LogQuery query, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
