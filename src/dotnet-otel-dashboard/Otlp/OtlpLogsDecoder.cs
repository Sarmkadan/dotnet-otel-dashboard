namespace DotnetOtelDashboard.Otlp;

/// <summary>
/// Decodes OTLP <c>ExportLogsServiceRequest</c> payloads into <see cref="LogRecord"/> values, following
/// <c>opentelemetry/proto/collector/logs/v1/logs_service.proto</c> and <c>opentelemetry/proto/logs/v1/logs.proto</c>.
/// </summary>
/// <remarks>
/// Unknown fields are skipped. Malformed input throws <see cref="InvalidDataException"/>.
/// </remarks>
internal static class OtlpLogsDecoder
{
    // Largest tick count (100 ns units) that fits between the Unix epoch and DateTimeOffset.MaxValue.
    private static readonly ulong MaxUnixTicks = (ulong)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks);

    private const int TraceIdLength = 16;
    private const int SpanIdLength = 8;
    private const int MaxSeverityNumber = 24;

    /// <summary>
    /// Decodes an <c>ExportLogsServiceRequest</c> message into the log records it contains.
    /// </summary>
    /// <param name="exportLogsServiceRequest">Encoded <c>ExportLogsServiceRequest</c> bytes.</param>
    /// <returns>Log records in payload order. Empty when the request carries no log records.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    public static List<LogRecord> Decode(ReadOnlySpan<byte> exportLogsServiceRequest)
    {
        var records = new List<LogRecord>();
        var reader = new ProtobufReader(exportLogsServiceRequest);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // resource_logs = 1
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                DecodeResourceLogs(reader.ReadLengthDelimited(), records, 1);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return records;
    }

    private static void DecodeResourceLogs(ReadOnlySpan<byte> bytes, List<LogRecord> records, int depth)
    {
        ProtobufReader.ValidateDepth(depth);
        // The resource may appear after scope_logs on the wire, so locate it before decoding any log record.
        ReadOnlySpan<byte> resource = default;
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // resource = 1
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                resource = reader.ReadLengthDelimited();
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        string serviceName = OtlpAttributeDecoder.ExtractServiceName(resource);

        reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // scope_logs = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                DecodeScopeLogs(reader.ReadLengthDelimited(), serviceName, records, depth + 1);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static void DecodeScopeLogs(ReadOnlySpan<byte> bytes, string serviceName, List<LogRecord> records, int depth)
    {
        ProtobufReader.ValidateDepth(depth);
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // log_records = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                records.Add(DecodeLogRecord(reader.ReadLengthDelimited(), serviceName, depth + 1));
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static LogRecord DecodeLogRecord(ReadOnlySpan<byte> bytes, string serviceName, int depth)
    {
        ProtobufReader.ValidateDepth(depth);
        ulong timeUnixNanos = 0;
        ulong observedTimeUnixNanos = 0;
        int severity = 0;
        string severityText = string.Empty;
        string body = string.Empty;
        string? traceId = null;
        string? spanId = null;
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // time_unix_nano = 1 (fixed64)
                case (1, WireType.Fixed64):
                    timeUnixNanos = reader.ReadFixed64();
                    break;

                // severity_number = 2 (SeverityNumber enum)
                case (2, WireType.Varint):
                    severity = ToSeverityNumber(reader.ReadVarint());
                    break;

                // severity_text = 3
                case (3, WireType.LengthDelimited):
                    severityText = reader.ReadString();
                    break;

                // body = 5 (AnyValue)
                case (5, WireType.LengthDelimited):
                    body = OtlpAttributeDecoder.DecodeAnyValue(reader.ReadLengthDelimited());
                    break;

                // attributes = 6 (repeated KeyValue)
                case (6, WireType.LengthDelimited):
                    OtlpAttributeDecoder.AddKeyValue(attributes, reader.ReadLengthDelimited());
                    break;

                // trace_id = 9 (16 bytes, empty when not correlated)
                case (9, WireType.LengthDelimited):
                    traceId = ToOptionalTraceId(reader.ReadLengthDelimited());
                    break;

                // span_id = 10 (8 bytes, empty when not correlated)
                case (10, WireType.LengthDelimited):
                    spanId = ToOptionalSpanId(reader.ReadLengthDelimited(), "span_id");
                    break;

                // observed_time_unix_nano = 11 (fixed64)
                case (11, WireType.Fixed64):
                    observedTimeUnixNanos = reader.ReadFixed64();
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        // time_unix_nano is optional and 0 when unknown; fall back to the time the collector observed the record.
        ulong effectiveUnixNanos = timeUnixNanos != 0 ? timeUnixNanos : observedTimeUnixNanos;

        return new LogRecord(
            serviceName,
            ToDateTimeOffset(effectiveUnixNanos),
            severity,
            severityText,
            body,
            traceId,
            spanId,
            attributes);
    }

    // trace_id is optional (null when not correlated); when present it must be exactly 16 bytes.
    private static string? ToOptionalTraceId(ReadOnlySpan<byte> id)
    {
        if (id.IsEmpty)
        {
            return null;
        }

        if (id.Length != TraceIdLength)
        {
            throw new InvalidDataException($"trace_id must be {TraceIdLength} bytes but was {id.Length}.");
        }

        return ToHex(id);
    }

    // span_id is optional (null when not correlated); when present it must be exactly 8 bytes.
    private static string? ToOptionalSpanId(ReadOnlySpan<byte> id, string fieldName)
    {
        if (id.IsEmpty)
        {
            return null;
        }

        if (id.Length != SpanIdLength)
        {
            throw new InvalidDataException($"{fieldName} must be {SpanIdLength} bytes but was {id.Length}.");
        }

        return ToHex(id);
    }

    private static string ToHex(ReadOnlySpan<byte> id)
    {
        return Convert.ToHexString(id).ToLowerInvariant();
    }

    private static int ToSeverityNumber(ulong value)
    {
        // Out-of-range values are clamped to the SeverityNumber range rather than rejected.
        return value <= MaxSeverityNumber ? (int)value : MaxSeverityNumber;
    }

    private static DateTimeOffset ToDateTimeOffset(ulong unixNanos)
    {
        ulong ticks = unixNanos / 100;
        if (ticks > MaxUnixTicks)
        {
            throw new InvalidDataException($"Timestamp {unixNanos} ns is outside the supported range.");
        }

        return DateTimeOffset.UnixEpoch.AddTicks((long)ticks);
    }
}
