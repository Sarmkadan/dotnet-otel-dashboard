using DotnetOtelDashboard.Otlp;

namespace DotnetOtelDashboard.Tests.Otlp;

public class OtlpTraceDecoderTests
{
    private static readonly byte[] TraceIdBytes = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10];
    private static readonly byte[] SpanIdBytes = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7];
    private static readonly byte[] ParentSpanIdBytes = [0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7];

    private const ulong StartUnixNanos = 1_700_000_000_000_000_000UL;
    private const ulong DurationNanos = 250_000_000UL;
    private const ulong EndUnixNanos = StartUnixNanos + DurationNanos;

    [Fact]
    public void Decode_HandWrittenServerSpan_MapsEveryField()
    {
        byte[] payload = Request(ResourceSpans("checkout", BuildSpan()));

        SpanRecord span = Assert.Single(OtlpTraceDecoder.Decode(payload));

        Assert.Equal(Convert.ToHexString(TraceIdBytes).ToLowerInvariant(), span.TraceId);
        Assert.Equal(32, span.TraceId.Length);
        Assert.Equal(Convert.ToHexString(SpanIdBytes).ToLowerInvariant(), span.SpanId);
        Assert.Equal(16, span.SpanId.Length);
        Assert.Null(span.ParentSpanId);
        Assert.Equal("checkout", span.ServiceName);
        Assert.Equal("POST /checkout", span.Name);
        Assert.Equal(SpanKind.Server, span.Kind);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks((long)(StartUnixNanos / 100)), span.StartTime);
        Assert.Equal(TimeSpan.FromMilliseconds(250), span.Duration);
        Assert.Equal(SpanStatusCode.Error, span.StatusCode);
        Assert.Equal("200", span.Attributes["http.status_code"]);
    }

    [Fact]
    public void Decode_ParentSpanId_IsHexWhenPresent()
    {
        byte[] payload = Request(ResourceSpans("checkout", BuildSpan(ParentSpanIdBytes)));

        SpanRecord span = Assert.Single(OtlpTraceDecoder.Decode(payload));

        Assert.Equal(Convert.ToHexString(ParentSpanIdBytes).ToLowerInvariant(), span.ParentSpanId);
    }

    [Fact]
    public void Decode_TwoResourceSpans_AssignServiceNamePerResource()
    {
        byte[] payload = Request(
            ResourceSpans("checkout", BuildSpan()),
            ResourceSpans("payments", BuildSpan()));

        List<SpanRecord> spans = OtlpTraceDecoder.Decode(payload);

        Assert.Equal(2, spans.Count);
        Assert.Equal("checkout", spans[0].ServiceName);
        Assert.Equal("payments", spans[1].ServiceName);
    }

    [Fact]
    public void Decode_MissingServiceName_FallsBackToUnknownService()
    {
        byte[] payload = Request(ResourceSpans(null, BuildSpan()));

        SpanRecord span = Assert.Single(OtlpTraceDecoder.Decode(payload));

        Assert.Equal("unknown_service", span.ServiceName);
    }

    [Fact]
    public void Decode_EmptyPayload_ReturnsEmptyList()
    {
        List<SpanRecord> spans = OtlpTraceDecoder.Decode(ReadOnlySpan<byte>.Empty);

        Assert.Empty(spans);
    }

    [Fact]
    public void Decode_UnknownFieldInSpan_IsSkipped()
    {
        ProtobufWriter span = BuildSpan();
        span.WriteVarint(200, 42);
        byte[] payload = Request(ResourceSpans("checkout", span));

        SpanRecord decoded = Assert.Single(OtlpTraceDecoder.Decode(payload));

        Assert.Equal("POST /checkout", decoded.Name);
        Assert.Equal(SpanKind.Server, decoded.Kind);
        Assert.Equal(SpanStatusCode.Error, decoded.StatusCode);
    }

    // Builds a Span message with every field the decoder reads. Status.code is field 3 per the OTLP proto.
    private static ProtobufWriter BuildSpan(byte[]? parentSpanId = null)
    {
        var span = new ProtobufWriter();
        span.WriteBytes(1, TraceIdBytes);
        span.WriteBytes(2, SpanIdBytes);
        if (parentSpanId is not null)
        {
            span.WriteBytes(4, parentSpanId);
        }

        span.WriteString(5, "POST /checkout");
        span.WriteVarint(6, 2);
        span.WriteFixed64(7, StartUnixNanos);
        span.WriteFixed64(8, EndUnixNanos);
        span.WriteMessage(9, IntAttribute("http.status_code", 200));

        var status = new ProtobufWriter();
        status.WriteVarint(3, 2);
        span.WriteMessage(15, status);

        return span;
    }

    // ResourceSpans { resource(1) { attributes(1) }, scope_spans(2) { spans(2) } }
    private static ProtobufWriter ResourceSpans(string? serviceName, params ProtobufWriter[] spans)
    {
        var scopeSpans = new ProtobufWriter();
        foreach (ProtobufWriter span in spans)
        {
            scopeSpans.WriteMessage(2, span);
        }

        var resourceSpans = new ProtobufWriter();
        resourceSpans.WriteMessage(1, Resource(serviceName));
        resourceSpans.WriteMessage(2, scopeSpans);
        return resourceSpans;
    }

    private static ProtobufWriter Resource(string? serviceName)
    {
        var resource = new ProtobufWriter();
        if (serviceName is not null)
        {
            resource.WriteMessage(1, StringAttribute("service.name", serviceName));
        }

        resource.WriteMessage(1, StringAttribute("host.name", "node-1"));
        return resource;
    }

    // ExportTraceServiceRequest { resource_spans(1) }
    private static byte[] Request(params ProtobufWriter[] resourceSpans)
    {
        var request = new ProtobufWriter();
        foreach (ProtobufWriter resourceSpan in resourceSpans)
        {
            request.WriteMessage(1, resourceSpan);
        }

        return request.ToArray();
    }

    private static ProtobufWriter StringAttribute(string key, string value)
    {
        var anyValue = new ProtobufWriter();
        anyValue.WriteString(1, value);
        return KeyValue(key, anyValue);
    }

    private static ProtobufWriter IntAttribute(string key, ulong value)
    {
        var anyValue = new ProtobufWriter();
        anyValue.WriteVarint(3, value);
        return KeyValue(key, anyValue);
    }

    private static ProtobufWriter KeyValue(string key, ProtobufWriter anyValue)
    {
        var keyValue = new ProtobufWriter();
        keyValue.WriteString(1, key);
        keyValue.WriteMessage(2, anyValue);
        return keyValue;
    }
}
