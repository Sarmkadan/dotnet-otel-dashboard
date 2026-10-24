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

/// <summary>
/// Span filter. Validated on construction and on every <c>with</c> assignment. Each assigned bound is checked
/// against the current value of the other bound, so moving both bounds in one <c>with</c> is checked step by step.
/// </summary>
public sealed record SpanQuery
{
    private DateTimeOffset? _from;
    private DateTimeOffset? _to;
    private int _limit;

    /// <summary>Creates a span filter.</summary>
    /// <param name="ServiceName">Restrict to this service, or <see langword="null"/> for all.</param>
    /// <param name="SpanName">Restrict to this span name, or <see langword="null"/> for all.</param>
    /// <param name="MinDuration">Only spans at least this long, or <see langword="null"/>.</param>
    /// <param name="ErrorsOnly">Only spans with <see cref="SpanStatusCode.Error"/>.</param>
    /// <param name="From">Inclusive lower bound on start time, or <see langword="null"/>.</param>
    /// <param name="To">Exclusive upper bound on start time, or <see langword="null"/>.</param>
    /// <param name="Limit">Maximum number of results. Default 100.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Limit"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="From"/> is later than <paramref name="To"/>.</exception>
    public SpanQuery(
        string? ServiceName = null,
        string? SpanName = null,
        TimeSpan? MinDuration = null,
        bool ErrorsOnly = false,
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        int Limit = 100)
    {
        QueryGuards.ThrowIfInverted(From, To, nameof(To));
        this.ServiceName = ServiceName;
        this.SpanName = SpanName;
        this.MinDuration = MinDuration;
        this.ErrorsOnly = ErrorsOnly;
        _from = From;
        _to = To;
        this.Limit = Limit;
    }

    /// <summary>Restrict to this service, or <see langword="null"/> for all.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Restrict to this span name, or <see langword="null"/> for all.</summary>
    public string? SpanName { get; init; }

    /// <summary>Only spans at least this long, or <see langword="null"/>.</summary>
    public TimeSpan? MinDuration { get; init; }

    /// <summary>Only spans with <see cref="SpanStatusCode.Error"/>.</summary>
    public bool ErrorsOnly { get; init; }

    /// <summary>Inclusive lower bound on start time, or <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException">The value is later than <see cref="To"/>.</exception>
    public DateTimeOffset? From
    {
        get => _from;
        init
        {
            QueryGuards.ThrowIfInverted(value, _to, nameof(From));
            _from = value;
        }
    }

    /// <summary>Exclusive upper bound on start time, or <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException">The value is earlier than <see cref="From"/>.</exception>
    public DateTimeOffset? To
    {
        get => _to;
        init
        {
            QueryGuards.ThrowIfInverted(_from, value, nameof(To));
            _to = value;
        }
    }

    /// <summary>Maximum number of results. Default 100.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int Limit
    {
        get => _limit;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(Limit));
            _limit = value;
        }
    }
}

/// <summary>
/// Metric filter. Validated on construction and on every <c>with</c> assignment. Each assigned bound is checked
/// against the current value of the other bound, so moving both bounds in one <c>with</c> is checked step by step.
/// </summary>
public sealed record MetricQuery
{
    private string _metricName;
    private DateTimeOffset _from;
    private DateTimeOffset _to;

    /// <summary>Creates a metric filter.</summary>
    /// <param name="MetricName">Instrument name (required).</param>
    /// <param name="ServiceName">Restrict to this service, or <see langword="null"/> for all.</param>
    /// <param name="From">Inclusive lower bound on timestamp.</param>
    /// <param name="To">Exclusive upper bound on timestamp.</param>
    /// <exception cref="ArgumentException"><paramref name="MetricName"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentException"><paramref name="From"/> is later than <paramref name="To"/>.</exception>
    public MetricQuery(string MetricName, string? ServiceName, DateTimeOffset From, DateTimeOffset To)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(MetricName);
        QueryGuards.ThrowIfInverted(From, To, nameof(To));
        _metricName = MetricName;
        this.ServiceName = ServiceName;
        _from = From;
        _to = To;
    }

    /// <summary>Instrument name (required).</summary>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public string MetricName
    {
        get => _metricName;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(MetricName));
            _metricName = value;
        }
    }

    /// <summary>Restrict to this service, or <see langword="null"/> for all.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Inclusive lower bound on timestamp.</summary>
    /// <exception cref="ArgumentException">The value is later than <see cref="To"/>.</exception>
    public DateTimeOffset From
    {
        get => _from;
        init
        {
            QueryGuards.ThrowIfInverted(value, _to, nameof(From));
            _from = value;
        }
    }

    /// <summary>Exclusive upper bound on timestamp.</summary>
    /// <exception cref="ArgumentException">The value is earlier than <see cref="From"/>.</exception>
    public DateTimeOffset To
    {
        get => _to;
        init
        {
            QueryGuards.ThrowIfInverted(_from, value, nameof(To));
            _to = value;
        }
    }
}

/// <summary>
/// Log filter. Validated on construction and on every <c>with</c> assignment. Each assigned bound is checked
/// against the current value of the other bound, so moving both bounds in one <c>with</c> is checked step by step.
/// </summary>
public sealed record LogQuery
{
    private DateTimeOffset? _from;
    private DateTimeOffset? _to;
    private int _limit;

    /// <summary>Creates a log filter.</summary>
    /// <param name="ServiceName">Restrict to this service, or <see langword="null"/> for all.</param>
    /// <param name="MinSeverity">Minimum severity number, or <see langword="null"/>.</param>
    /// <param name="TraceId">Only records correlated to this trace, or <see langword="null"/>.</param>
    /// <param name="Search">Case-insensitive substring match on <see cref="LogRecord.Body"/>, or <see langword="null"/>.</param>
    /// <param name="From">Inclusive lower bound on timestamp, or <see langword="null"/>.</param>
    /// <param name="To">Exclusive upper bound on timestamp, or <see langword="null"/>.</param>
    /// <param name="Limit">Maximum number of results. Default 100.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Limit"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="From"/> is later than <paramref name="To"/>.</exception>
    public LogQuery(
        string? ServiceName = null,
        int? MinSeverity = null,
        string? TraceId = null,
        string? Search = null,
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        int Limit = 100)
    {
        QueryGuards.ThrowIfInverted(From, To, nameof(To));
        this.ServiceName = ServiceName;
        this.MinSeverity = MinSeverity;
        this.TraceId = TraceId;
        this.Search = Search;
        _from = From;
        _to = To;
        this.Limit = Limit;
    }

    /// <summary>Restrict to this service, or <see langword="null"/> for all.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Minimum severity number, or <see langword="null"/>.</summary>
    public int? MinSeverity { get; init; }

    /// <summary>Only records correlated to this trace, or <see langword="null"/>.</summary>
    public string? TraceId { get; init; }

    /// <summary>Case-insensitive substring match on <see cref="LogRecord.Body"/>, or <see langword="null"/>.</summary>
    public string? Search { get; init; }

    /// <summary>Inclusive lower bound on timestamp, or <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException">The value is later than <see cref="To"/>.</exception>
    public DateTimeOffset? From
    {
        get => _from;
        init
        {
            QueryGuards.ThrowIfInverted(value, _to, nameof(From));
            _from = value;
        }
    }

    /// <summary>Exclusive upper bound on timestamp, or <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException">The value is earlier than <see cref="From"/>.</exception>
    public DateTimeOffset? To
    {
        get => _to;
        init
        {
            QueryGuards.ThrowIfInverted(_from, value, nameof(To));
            _to = value;
        }
    }

    /// <summary>Maximum number of results. Default 100.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int Limit
    {
        get => _limit;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(Limit));
            _limit = value;
        }
    }
}

/// <summary>Shared checks for the query records.</summary>
internal static class QueryGuards
{
    /// <summary>Throws when both bounds are set and <paramref name="from"/> is later than <paramref name="to"/>.</summary>
    /// <param name="from">Lower bound, or <see langword="null"/>.</param>
    /// <param name="to">Upper bound, or <see langword="null"/>.</param>
    /// <param name="paramName">Parameter name reported on the exception.</param>
    public static void ThrowIfInverted(DateTimeOffset? from, DateTimeOffset? to, string paramName)
    {
        if (from is { } lower && to is { } upper && lower > upper)
        {
            throw new ArgumentException("From must not be later than To.", paramName);
        }
    }
}
