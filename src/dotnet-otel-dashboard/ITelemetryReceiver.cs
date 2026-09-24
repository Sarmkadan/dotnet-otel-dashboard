namespace DotnetOtelDashboard;

/// <summary>
/// Ingests OTLP payloads and forwards decoded records to the <see cref="ITelemetryStore"/>.
/// One instance handles all three signals.
/// </summary>
public interface ITelemetryReceiver
{
    /// <summary>
    /// Decodes an OTLP <c>ExportTraceServiceRequest</c> and stores the spans.
    /// </summary>
    /// <param name="payload">Raw protobuf bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of spans accepted.</returns>
    ValueTask<int> ReceiveTracesAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes an OTLP <c>ExportMetricsServiceRequest</c> and stores the data points.
    /// </summary>
    /// <param name="payload">Raw protobuf bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of data points accepted.</returns>
    ValueTask<int> ReceiveMetricsAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes an OTLP <c>ExportLogsServiceRequest</c> and stores the log records.
    /// </summary>
    /// <param name="payload">Raw protobuf bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of log records accepted.</returns>
    ValueTask<int> ReceiveLogsAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
}
