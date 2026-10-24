namespace DotnetOtelDashboard.Tests;

/// <summary>
/// Test double for <see cref="ITelemetryStore"/> that records every write batch. Query methods return empty results.
/// </summary>
public sealed class RecordingTelemetryStore : ITelemetryStore
{
    /// <summary>Span batches in the order they were written. Each entry is the list passed to one call.</summary>
    public List<IReadOnlyList<SpanRecord>> SpanBatches { get; } = [];

    /// <summary>Metric batches in the order they were written.</summary>
    public List<IReadOnlyList<MetricPoint>> MetricBatches { get; } = [];

    /// <summary>Log batches in the order they were written.</summary>
    public List<IReadOnlyList<LogRecord>> LogBatches { get; } = [];

    /// <inheritdoc />
    public ValueTask WriteSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken cancellationToken = default)
    {
        SpanBatches.Add(spans);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteMetricsAsync(IReadOnlyList<MetricPoint> points, CancellationToken cancellationToken = default)
    {
        MetricBatches.Add(points);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteLogsAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default)
    {
        LogBatches.Add(records);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpanRecord>> QuerySpansAsync(SpanQuery query, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<SpanRecord>>(new List<SpanRecord>());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpanRecord>> GetTraceAsync(string traceId, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<SpanRecord>>(new List<SpanRecord>());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<MetricPoint>> QueryMetricsAsync(MetricQuery query, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<MetricPoint>>(new List<MetricPoint>());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LogRecord>> QueryLogsAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<LogRecord>>(new List<LogRecord>());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<string>>(new List<string>());
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        SpanBatches.Clear();
        MetricBatches.Clear();
        LogBatches.Clear();
        return ValueTask.CompletedTask;
    }
}
