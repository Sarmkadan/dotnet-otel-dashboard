namespace DotnetOtelDashboard;

/// <summary>
/// Storage abstraction for all three signals. Implementations must be thread-safe and bounded according to
/// <see cref="OtelDashboardOptions"/>.
/// </summary>
public interface ITelemetryStore
{
    /// <summary>Writes a batch of spans.</summary>
    /// <param name="spans">Spans to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask WriteSpansAsync(IReadOnlyList<SpanRecord> spans, CancellationToken cancellationToken = default);

    /// <summary>Writes a batch of metric points.</summary>
    /// <param name="points">Points to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask WriteMetricsAsync(IReadOnlyList<MetricPoint> points, CancellationToken cancellationToken = default);

    /// <summary>Writes a batch of log records.</summary>
    /// <param name="records">Records to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask WriteLogsAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default);

    /// <summary>Queries spans matching <paramref name="query"/>, newest first.</summary>
    /// <param name="query">Filter and paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching spans, at most <see cref="SpanQuery.Limit"/>.</returns>
    ValueTask<IReadOnlyList<SpanRecord>> QuerySpansAsync(SpanQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns all spans belonging to one trace, ordered by start time.</summary>
    /// <param name="traceId">Trace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Spans of the trace, or an empty list when the trace is unknown.</returns>
    ValueTask<IReadOnlyList<SpanRecord>> GetTraceAsync(string traceId, CancellationToken cancellationToken = default);

    /// <summary>Queries metric points for one instrument within a time range.</summary>
    /// <param name="query">Filter parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching points ordered by timestamp.</returns>
    ValueTask<IReadOnlyList<MetricPoint>> QueryMetricsAsync(MetricQuery query, CancellationToken cancellationToken = default);

    /// <summary>Queries log records, newest first.</summary>
    /// <param name="query">Filter and paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching records, at most <see cref="LogQuery.Limit"/>.</returns>
    ValueTask<IReadOnlyList<LogRecord>> QueryLogsAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns the distinct service names seen across all signals.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes all stored telemetry.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Span filter.</summary>
/// <param name="ServiceName">Restrict to this service, or <see langword="null"/> for all.</param>
/// <param name="SpanName">Restrict to this span name, or <see langword="null"/> for all.</param>
/// <param name="MinDuration">Only spans at least this long, or <see langword="null"/>.</param>
/// <param name="ErrorsOnly">Only spans with <see cref="SpanStatusCode.Error"/>.</param>
/// <param name="From">Inclusive lower bound on start time, or <see langword="null"/>.</param>
/// <param name="To">Exclusive upper bound on start time, or <see langword="null"/>.</param>
/// <param name="Limit">Maximum number of results. Default 100.</param>
public sealed record SpanQuery(
    string? ServiceName = null,
    string? SpanName = null,
    TimeSpan? MinDuration = null,
    bool ErrorsOnly = false,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100);

/// <summary>Metric filter.</summary>
/// <param name="MetricName">Instrument name (required).</param>
/// <param name="ServiceName">Restrict to this service, or <see langword="null"/> for all.</param>
/// <param name="From">Inclusive lower bound on timestamp.</param>
/// <param name="To">Exclusive upper bound on timestamp.</param>
public sealed record MetricQuery(
    string MetricName,
    string? ServiceName,
    DateTimeOffset From,
    DateTimeOffset To);

/// <summary>Log filter.</summary>
/// <param name="ServiceName">Restrict to this service, or <see langword="null"/> for all.</param>
/// <param name="MinSeverity">Minimum severity number, or <see langword="null"/>.</param>
/// <param name="TraceId">Only records correlated to this trace, or <see langword="null"/>.</param>
/// <param name="Search">Case-insensitive substring match on <see cref="LogRecord.Body"/>, or <see langword="null"/>.</param>
/// <param name="From">Inclusive lower bound on timestamp, or <see langword="null"/>.</param>
/// <param name="To">Exclusive upper bound on timestamp, or <see langword="null"/>.</param>
/// <param name="Limit">Maximum number of results. Default 100.</param>
public sealed record LogQuery(
    string? ServiceName = null,
    int? MinSeverity = null,
    string? TraceId = null,
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100);
