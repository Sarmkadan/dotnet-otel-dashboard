namespace DotnetOtelDashboard;

/// <summary>
/// A single span as stored by the dashboard. Protobuf types are decoded into this shape at ingest.
/// </summary>
/// <param name="TraceId">32-character lowercase hex trace identifier.</param>
/// <param name="SpanId">16-character lowercase hex span identifier.</param>
/// <param name="ParentSpanId">Parent span identifier, or <see langword="null"/> for a root span.</param>
/// <param name="ServiceName">Value of the <c>service.name</c> resource attribute.</param>
/// <param name="Name">Span name.</param>
/// <param name="Kind">Span kind as defined by OpenTelemetry.</param>
/// <param name="StartTime">Start timestamp in UTC.</param>
/// <param name="Duration">Span duration.</param>
/// <param name="StatusCode">Span status: unset, ok or error.</param>
/// <param name="Attributes">Span attributes flattened to string values.</param>
public sealed record SpanRecord(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string ServiceName,
    string Name,
    SpanKind Kind,
    DateTimeOffset StartTime,
    TimeSpan Duration,
    SpanStatusCode StatusCode,
    IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// OpenTelemetry span kind.
/// </summary>
public enum SpanKind
{
    /// <summary>Unspecified.</summary>
    Unspecified = 0,
    /// <summary>Internal operation.</summary>
    Internal = 1,
    /// <summary>Server-side handling of a request.</summary>
    Server = 2,
    /// <summary>Client-side outgoing request.</summary>
    Client = 3,
    /// <summary>Message producer.</summary>
    Producer = 4,
    /// <summary>Message consumer.</summary>
    Consumer = 5,
}

/// <summary>
/// OpenTelemetry span status code.
/// </summary>
public enum SpanStatusCode
{
    /// <summary>Status not set.</summary>
    Unset = 0,
    /// <summary>Operation completed successfully.</summary>
    Ok = 1,
    /// <summary>Operation failed.</summary>
    Error = 2,
}

/// <summary>
/// A single metric data point.
/// </summary>
/// <param name="ServiceName">Value of the <c>service.name</c> resource attribute.</param>
/// <param name="MetricName">Instrument name.</param>
/// <param name="Unit">Instrument unit, may be empty.</param>
/// <param name="Type">Instrument type.</param>
/// <param name="Timestamp">Point timestamp in UTC.</param>
/// <param name="Value">Numeric value. For histograms this is the sum; buckets are in <paramref name="HistogramBuckets"/>.</param>
/// <param name="Attributes">Data point attributes flattened to string values.</param>
/// <param name="HistogramBuckets">Histogram bucket counts keyed by upper bound, or <see langword="null"/>.</param>
public sealed record MetricPoint(
    string ServiceName,
    string MetricName,
    string Unit,
    MetricType Type,
    DateTimeOffset Timestamp,
    double Value,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<double, long>? HistogramBuckets);

/// <summary>
/// OpenTelemetry metric instrument type.
/// </summary>
public enum MetricType
{
    /// <summary>Monotonic or non-monotonic sum.</summary>
    Sum = 0,
    /// <summary>Gauge.</summary>
    Gauge = 1,
    /// <summary>Histogram.</summary>
    Histogram = 2,
}

/// <summary>
/// A single log record, optionally correlated to a span.
/// </summary>
/// <param name="ServiceName">Value of the <c>service.name</c> resource attribute.</param>
/// <param name="Timestamp">Record timestamp in UTC.</param>
/// <param name="Severity">Severity number as defined by OpenTelemetry (1-24).</param>
/// <param name="SeverityText">Severity text, e.g. <c>Information</c>.</param>
/// <param name="Body">Rendered log message.</param>
/// <param name="TraceId">Correlated trace id, or <see langword="null"/>.</param>
/// <param name="SpanId">Correlated span id, or <see langword="null"/>.</param>
/// <param name="Attributes">Log attributes flattened to string values.</param>
public sealed record LogRecord(
    string ServiceName,
    DateTimeOffset Timestamp,
    int Severity,
    string SeverityText,
    string Body,
    string? TraceId,
    string? SpanId,
    IReadOnlyDictionary<string, string> Attributes);
