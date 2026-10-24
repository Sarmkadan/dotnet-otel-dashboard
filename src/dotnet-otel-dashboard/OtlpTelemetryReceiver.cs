using DotnetOtelDashboard.Otlp;

namespace DotnetOtelDashboard;

/// <summary>
/// Default <see cref="ITelemetryReceiver"/>: decodes OTLP/protobuf payloads and writes the resulting records into an
/// <see cref="ITelemetryStore"/>.
/// </summary>
public sealed class OtlpTelemetryReceiver : ITelemetryReceiver
{
    /// <summary>
    /// Largest payload, in bytes, accepted by a single <c>Receive*Async</c> call. Larger payloads are rejected before
    /// any decoding takes place.
    /// </summary>
    public const int MaxPayloadBytes = 16 * 1024 * 1024;

    private readonly ITelemetryStore _store;

    /// <summary>
    /// Creates a receiver that writes decoded telemetry into <paramref name="store"/>.
    /// </summary>
    /// <param name="store">Destination store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public OtlpTelemetryReceiver(ITelemetryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Decodes an OTLP <c>ExportTraceServiceRequest</c> and stores the spans. Nothing is written when the request
    /// carries no spans.
    /// </summary>
    /// <param name="payload">Raw protobuf bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of spans accepted.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payload"/> is longer than <see cref="MaxPayloadBytes"/>.</exception>
    public ValueTask<int> ReceiveTracesAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ValidatePayloadSize(payload);
        return ReceiveTracesCoreAsync(payload, cancellationToken);
    }

    private async ValueTask<int> ReceiveTracesCoreAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        List<SpanRecord> spans;
        try
        {
            spans = OtlpTraceDecoder.Decode(payload.Span);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Malformed OTLP traces payload.", ex);
        }

        if (spans.Count == 0)
        {
            return 0;
        }

        await _store.WriteSpansAsync(spans, cancellationToken);
        return spans.Count;
    }

    /// <summary>
    /// Decodes an OTLP <c>ExportMetricsServiceRequest</c> and stores the data points. Nothing is written when the
    /// request carries no data points.
    /// </summary>
    /// <param name="payload">Raw protobuf bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of data points accepted.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payload"/> is longer than <see cref="MaxPayloadBytes"/>.</exception>
    public ValueTask<int> ReceiveMetricsAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ValidatePayloadSize(payload);
        return ReceiveMetricsCoreAsync(payload, cancellationToken);
    }

    private async ValueTask<int> ReceiveMetricsCoreAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        List<MetricPoint> points;
        try
        {
            points = OtlpMetricsDecoder.Decode(payload.Span);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Malformed OTLP metrics payload.", ex);
        }

        if (points.Count == 0)
        {
            return 0;
        }

        await _store.WriteMetricsAsync(points, cancellationToken);
        return points.Count;
    }

    /// <summary>
    /// Decodes an OTLP <c>ExportLogsServiceRequest</c> and stores the log records. Nothing is written when the request
    /// carries no records.
    /// </summary>
    /// <param name="payload">Raw protobuf bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of log records accepted.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payload"/> is longer than <see cref="MaxPayloadBytes"/>.</exception>
    public ValueTask<int> ReceiveLogsAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ValidatePayloadSize(payload);
        return ReceiveLogsCoreAsync(payload, cancellationToken);
    }

    private async ValueTask<int> ReceiveLogsCoreAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        List<LogRecord> records;
        try
        {
            records = OtlpLogsDecoder.Decode(payload.Span);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Malformed OTLP logs payload.", ex);
        }

        if (records.Count == 0)
        {
            return 0;
        }

        await _store.WriteLogsAsync(records, cancellationToken);
        return records.Count;
    }

    private static void ValidatePayloadSize(ReadOnlyMemory<byte> payload)
    {
        // Checked synchronously so an oversized payload fails at the call site, before any await.
        if (payload.Length > MaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }
    }
}
