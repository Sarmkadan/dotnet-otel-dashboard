namespace DotnetOtelDashboard;

/// <summary>
/// Configuration for the OpenTelemetry dashboard: retention, size limits and hosting paths.
/// </summary>
public sealed class OtelDashboardOptions
{
    /// <summary>
    /// Configuration section name used when binding from appsettings, e.g.
    /// <c>builder.Configuration.GetSection(OtelDashboardOptions.SectionName)</c>. Matches the
    /// <c>OtelDashboard</c> key in <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "OtelDashboard";

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
    /// Validates the options and throws if any value is out of range. On success, normalizes
    /// <see cref="ReceiverBasePath"/> by removing trailing slashes (the root path <c>/</c> is kept as-is).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Retention"/> or a size limit is not greater than zero.</exception>
    /// <exception cref="ArgumentException"><see cref="ReceiverBasePath"/> is null, whitespace, not rooted at <c>/</c>, or contains whitespace, <c>?</c> or <c>#</c>;
    /// or <see cref="AuthorizationPolicy"/> is non-null but whitespace.</exception>
    public void Validate()
    {
        if (Retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Retention), Retention, "Retention must be greater than zero.");
        }

        if (MaxSpans <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxSpans), MaxSpans, "MaxSpans must be greater than zero.");
        }

        if (MaxMetricPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMetricPoints), MaxMetricPoints, "MaxMetricPoints must be greater than zero.");
        }

        if (MaxLogRecords <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxLogRecords), MaxLogRecords, "MaxLogRecords must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(ReceiverBasePath))
        {
            throw new ArgumentException("ReceiverBasePath must not be null or whitespace.", nameof(ReceiverBasePath));
        }

        if (ReceiverBasePath[0] != '/')
        {
            throw new ArgumentException($"ReceiverBasePath must start with '/' but was '{ReceiverBasePath}'.", nameof(ReceiverBasePath));
        }

        foreach (var c in ReceiverBasePath)
        {
            if (char.IsWhiteSpace(c) || c is '?' or '#')
            {
                throw new ArgumentException($"ReceiverBasePath must not contain whitespace, '?' or '#' but was '{ReceiverBasePath}'.", nameof(ReceiverBasePath));
            }
        }

        ReceiverBasePath = ReceiverBasePath.Length > 1 ? ReceiverBasePath.TrimEnd('/') : ReceiverBasePath;

        if (AuthorizationPolicy is not null && string.IsNullOrWhiteSpace(AuthorizationPolicy))
        {
            throw new ArgumentException("AuthorizationPolicy must be null or non-whitespace.", nameof(AuthorizationPolicy));
        }
    }
}
