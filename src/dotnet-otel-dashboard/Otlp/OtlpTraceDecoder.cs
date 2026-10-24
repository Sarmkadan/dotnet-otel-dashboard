namespace DotnetOtelDashboard.Otlp;

/// <summary>
/// Decodes OTLP <c>ExportTraceServiceRequest</c> payloads into <see cref="SpanRecord"/> values, following
/// <c>opentelemetry/proto/collector/trace/v1/trace_service.proto</c> and <c>opentelemetry/proto/trace/v1/trace.proto</c>.
/// </summary>
/// <remarks>
/// Unknown fields are skipped. Malformed input throws <see cref="InvalidDataException"/>.
/// </remarks>
internal static class OtlpTraceDecoder
{
    // Largest tick count (100 ns units) that fits between the Unix epoch and DateTimeOffset.MaxValue.
    private static readonly ulong MaxUnixTicks = (ulong)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks);

    /// <summary>
    /// Decodes an <c>ExportTraceServiceRequest</c> message into the spans it contains.
    /// </summary>
    /// <param name="exportTraceServiceRequest">Encoded <c>ExportTraceServiceRequest</c> bytes.</param>
    /// <returns>Spans in payload order. Empty when the request carries no spans.</returns>
    /// <exception cref="InvalidDataException">The payload is malformed.</exception>
    public static List<SpanRecord> Decode(ReadOnlySpan<byte> exportTraceServiceRequest)
    {
        var spans = new List<SpanRecord>();
        var reader = new ProtobufReader(exportTraceServiceRequest);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // resource_spans = 1
            if (fieldNumber == 1 && wireType == WireType.LengthDelimited)
            {
                DecodeResourceSpans(reader.ReadLengthDelimited(), spans);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return spans;
    }

    private static void DecodeResourceSpans(ReadOnlySpan<byte> bytes, List<SpanRecord> spans)
    {
        // The resource may appear after scope_spans on the wire, so locate it before decoding any span.
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
            // scope_spans = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                DecodeScopeSpans(reader.ReadLengthDelimited(), serviceName, spans);
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static void DecodeScopeSpans(ReadOnlySpan<byte> bytes, string serviceName, List<SpanRecord> spans)
    {
        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // spans = 2
            if (fieldNumber == 2 && wireType == WireType.LengthDelimited)
            {
                spans.Add(DecodeSpan(reader.ReadLengthDelimited(), serviceName));
            }
            else
            {
                reader.SkipField(wireType);
            }
        }
    }

    private static SpanRecord DecodeSpan(ReadOnlySpan<byte> bytes, string serviceName)
    {
        string traceId = string.Empty;
        string spanId = string.Empty;
        string? parentSpanId = null;
        string name = string.Empty;
        SpanKind kind = SpanKind.Unspecified;
        ulong startUnixNanos = 0;
        ulong endUnixNanos = 0;
        SpanStatusCode statusCode = SpanStatusCode.Unset;
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        var reader = new ProtobufReader(bytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            switch (fieldNumber, wireType)
            {
                // trace_id = 1 (16 bytes)
                case (1, WireType.LengthDelimited):
                    traceId = ToHex(reader.ReadLengthDelimited());
                    break;

                // span_id = 2 (8 bytes)
                case (2, WireType.LengthDelimited):
                    spanId = ToHex(reader.ReadLengthDelimited());
                    break;

                // parent_span_id = 4 (8 bytes, empty for root spans)
                case (4, WireType.LengthDelimited):
                    parentSpanId = ToHexOrNull(reader.ReadLengthDelimited());
                    break;

                // name = 5
                case (5, WireType.LengthDelimited):
                    name = reader.ReadString();
                    break;

                // kind = 6
                case (6, WireType.Varint):
                    kind = ToSpanKind(reader.ReadVarint());
                    break;

                // start_time_unix_nano = 7
                case (7, WireType.Fixed64):
                    startUnixNanos = reader.ReadFixed64();
                    break;

                // end_time_unix_nano = 8
                case (8, WireType.Fixed64):
                    endUnixNanos = reader.ReadFixed64();
                    break;

                // attributes = 9 (repeated KeyValue)
                case (9, WireType.LengthDelimited):
                    OtlpAttributeDecoder.AddKeyValue(attributes, reader.ReadLengthDelimited());
                    break;

                // status = 15
                case (15, WireType.LengthDelimited):
                    statusCode = DecodeStatusCode(reader.ReadLengthDelimited());
                    break;

                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        DateTimeOffset startTime = ToDateTimeOffset(startUnixNanos);
        TimeSpan duration = ToDateTimeOffset(endUnixNanos) - startTime;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return new SpanRecord(
            traceId,
            spanId,
            parentSpanId,
            serviceName,
            name,
            kind,
            startTime,
            duration,
            statusCode,
            attributes);
    }

    private static SpanStatusCode DecodeStatusCode(ReadOnlySpan<byte> statusBytes)
    {
        SpanStatusCode statusCode = SpanStatusCode.Unset;
        var reader = new ProtobufReader(statusBytes);
        while (reader.TryReadTag(out int fieldNumber, out WireType wireType))
        {
            // Status.code = 3 (StatusCode enum). Field 2 is the message string.
            if (fieldNumber == 3 && wireType == WireType.Varint)
            {
                statusCode = ToStatusCode(reader.ReadVarint());
            }
            else
            {
                reader.SkipField(wireType);
            }
        }

        return statusCode;
    }

    private static string ToHex(ReadOnlySpan<byte> id)
    {
        return Convert.ToHexString(id).ToLowerInvariant();
    }

    private static string? ToHexOrNull(ReadOnlySpan<byte> id)
    {
        return id.IsEmpty ? null : ToHex(id);
    }

    private static SpanKind ToSpanKind(ulong value)
    {
        // Values added to the proto by newer specification versions are reported as Unspecified.
        return value <= (ulong)SpanKind.Consumer ? (SpanKind)value : SpanKind.Unspecified;
    }

    private static SpanStatusCode ToStatusCode(ulong value)
    {
        // Values added to the proto by newer specification versions are reported as Unset.
        return value <= (ulong)SpanStatusCode.Error ? (SpanStatusCode)value : SpanStatusCode.Unset;
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
