namespace DotnetOtelDashboard;

/// <summary>
/// Configuration for the OpenTelemetry dashboard: retention, size limits and hosting paths.
/// </summary>
public sealed class OtelDashboardOptions
{
    /// <summary>
    /// How long telemetry is kept before being evicted. Default: 1 hour.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum number of spans held in the store at once. Oldest spans are evicted first. Default: 100 000.
    /// </summary>
    public int MaxSpans { get; set; } = 100_000;

    /// <summary>
    /// Maximum number of metric data points held in the store at once. Default: 500 000.
    /// </summary>
    public int MaxMetricPoints { get; set; } = 500_000;

    /// <summary>
    /// Maximum number of log records held in the store at once. Default: 100 000.
    /// </summary>
    public int MaxLogRecords { get; set; } = 100_000;

    /// <summary>
    /// Base path where the OTLP receiver endpoints (<c>/v1/traces</c>, <c>/v1/metrics</c>, <c>/v1/logs</c>)
    /// are mapped. Default: <c>/</c>, matching the OTLP/HTTP specification.
    /// </summary>
    public string ReceiverBasePath { get; set; } = "/";

    /// <summary>
    /// Whether the OTLP/gRPC receiver is enabled in addition to OTLP/HTTP. Default: <see langword="true"/>.
    /// </summary>
    public bool EnableGrpcReceiver { get; set; } = true;

    /// <summary>
    /// Optional authorization policy name applied to the dashboard UI and query endpoints.
    /// <see langword="null"/> means anonymous access.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Validates the options and throws if any value is out of range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit is non-positive or retention is non-positive.</exception>
    /// <exception cref="ArgumentException"><see cref="ReceiverBasePath"/> is not a valid absolute path.</exception>
    public void Validate() => throw new NotImplementedException();
}
