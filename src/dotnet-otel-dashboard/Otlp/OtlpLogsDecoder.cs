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
                DecodeResourceLogs(reader.ReadLengthDelimited(), records);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return records;
    }

    private static void DecodeResourceLogs(ReadOnlySpan<byte> bytes, List<LogRecord> records)
    {
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
                DecodeScopeLogs(reader.ReadLengthDelimited(), serviceName, records);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static void DecodeScopeLogs(ReadOnlySpan<byte> bytes, string serviceName, List<LogRecord> records)
    {
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // log_records = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                records.Add(DecodeLogRecord(reader.ReadLengthDelimited(), serviceName));
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static LogRecord DecodeLogRecord(ReadOnlySpan<byte> bytes, string serviceName)
    {
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
                    traceId = ToHexOrNull(reader.ReadLengthDelimited());
                    break;

                // span_id = 10 (8 bytes, empty when not correlated)
                case (10, WireType.LengthDelimited):
                    spanId = ToHexOrNull(reader.ReadLengthDelimited());
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

    private static string ToHex(ReadOnlySpan<byte> id)
    {
        return Convert.ToHexString(id).ToLowerInvariant();
    }

    private static string? ToHexOrNull(ReadOnlySpan<byte> id)
    {
        return id.IsEmpty ? null : ToHex(id);
    }

    private static int ToSeverityNumber(ulong value)
    {
        // Values outside the int range cannot be valid severities; clamp rather than wrap to a negative number.
        return value <= int.MaxValue ? (int)value : int.MaxValue;
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
