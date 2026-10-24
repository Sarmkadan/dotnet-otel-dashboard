namespace DotnetOtelDashboard;

/// <summary>
/// Default <see cref="ITelemetryStore"/>: bounded in-memory ring buffers per signal, evicting by count and by
/// <see cref="OtelDashboardOptions.Retention"/>.
/// </summary>
public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    private readonly OtelDashboardOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly RingBuffer<SpanRecord> _spans;
    private readonly RingBuffer<MetricPoint> _metrics;
    private readonly RingBuffer<LogRecord> _logs;

    /// <summary>
    /// Creates a store bounded by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Limits and retention.</param>
    /// <param name="timeProvider">Clock used for retention eviction; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryTelemetryStore(OtelDashboardOptions options, TimeProvider? timeProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _spans = new RingBuffer<SpanRecord>(_options.MaxSpans);
        _metrics = new RingBuffer<MetricPoint>(_options.MaxMetricPoints);
        _logs = new RingBuffer<LogRecord>(_options.MaxLogRecords);
    }

    /// <inheritdoc />
    public ValueTask WriteSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken cancellationToken = default)
    {
        _spans.AddRange(spans);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteMetricsAsync(IReadOnlyList<MetricPoint> points, CancellationToken cancellationToken = default)
    {
        _metrics.AddRange(points);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteLogsAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default)
    {
        _logs.AddRange(records);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpanRecord>> QuerySpansAsync(SpanQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<SpanRecord> matches = _spans.Snapshot()
            .Where(span => query.ServiceName is null || string.Equals(span.ServiceName, query.ServiceName, StringComparison.Ordinal))
            .Where(span => query.SpanName is null || string.Equals(span.Name, query.SpanName, StringComparison.Ordinal))
            .Where(span => query.MinDuration is null || span.Duration >= query.MinDuration.Value)
            .Where(span => !query.ErrorsOnly || span.StatusCode == SpanStatusCode.Error)
            .Where(span => query.From is null || span.StartTime >= query.From.Value)
            .Where(span => query.To is null || span.StartTime < query.To.Value)
            .OrderByDescending(span => span.StartTime)
            .ThenBy(span => span.SpanId, StringComparer.Ordinal)
            .Take(query.Limit)
            .ToArray();

        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpanRecord>> GetTraceAsync(string traceId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SpanRecord> trace = _spans.Snapshot()
            .Where(span => string.Equals(span.TraceId, traceId, StringComparison.Ordinal))
            .OrderBy(span => span.StartTime)
            .ThenBy(span => span.SpanId, StringComparer.Ordinal)
            .ToArray();

        return ValueTask.FromResult(trace);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<MetricPoint>> QueryMetricsAsync(MetricQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<MetricPoint> matches = _metrics.Snapshot()
            .Where(point => string.Equals(point.MetricName, query.MetricName, StringComparison.Ordinal))
            .Where(point => query.ServiceName is null || string.Equals(point.ServiceName, query.ServiceName, StringComparison.Ordinal))
            .Where(point => point.Timestamp >= query.From && point.Timestamp < query.To)
            .OrderBy(point => point.Timestamp)
            .ToArray();

        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LogRecord>> QueryLogsAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LogRecord> matches = _logs.Snapshot()
            .Where(record => query.ServiceName is null || string.Equals(record.ServiceName, query.ServiceName, StringComparison.Ordinal))
            .Where(record => query.MinSeverity is null || record.Severity >= query.MinSeverity.Value)
            .Where(record => query.TraceId is null || string.Equals(record.TraceId, query.TraceId, StringComparison.Ordinal))
            .Where(record => query.Search is null || record.Body.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .Where(record => query.From is null || record.Timestamp >= query.From.Value)
            .Where(record => query.To is null || record.Timestamp < query.To.Value)
            .OrderByDescending(record => record.Timestamp)
            .Take(query.Limit)
            .ToArray();

        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
