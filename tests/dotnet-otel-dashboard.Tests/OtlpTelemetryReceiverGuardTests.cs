using DotnetOtelDashboard.Otlp;
using DotnetOtelDashboard.Tests.Otlp;

namespace DotnetOtelDashboard.Tests;

public class OtlpTelemetryReceiverGuardTests
{
    private static readonly byte[] TraceIdBytes = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];
    private static readonly byte[] SpanIdBytes = [0, 0, 0, 0, 0, 0, 0, 1];

    private const ulong StartUnixNanos = 1_700_000_000_000_000_000UL;
    private const ulong EndUnixNanos = StartUnixNanos + 250_000_000UL;

    [Fact]
    public void ReceiveTracesAsync_PayloadOverMaxSize_ThrowsSynchronouslyWithoutWriting()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = new byte[OtlpTelemetryReceiver.MaxPayloadBytes + 1];

        // Thrown from the call itself, not from a faulted task, so the caller never awaits an oversized payload.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => receiver.ReceiveTracesAsync(payload));

        Assert.Equal("payload", ex.ParamName);
        Assert.Empty(store.SpanBatches);
    }

    [Fact]
    public void ReceiveMetricsAsync_PayloadOverMaxSize_ThrowsSynchronouslyWithoutWriting()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = new byte[OtlpTelemetryReceiver.MaxPayloadBytes + 1];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => receiver.ReceiveMetricsAsync(payload));

        Assert.Equal("payload", ex.ParamName);
        Assert.Empty(store.MetricBatches);
    }

    [Fact]
    public void ReceiveLogsAsync_PayloadOverMaxSize_ThrowsSynchronouslyWithoutWriting()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = new byte[OtlpTelemetryReceiver.MaxPayloadBytes + 1];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => receiver.ReceiveLogsAsync(payload));

        Assert.Equal("payload", ex.ParamName);
        Assert.Empty(store.LogBatches);
    }

    [Fact]
    public async Task ReceiveTracesAsync_TraceIdOf15Bytes_ThrowsInvalidDataNamingTraceId()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = TracesRequest(Span(new byte[15], SpanIdBytes));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => receiver.ReceiveTracesAsync(payload).AsTask());

        Assert.Contains("trace_id", ex.InnerException!.Message);
        Assert.Empty(store.SpanBatches);
    }

    [Fact]
    public async Task ReceiveTracesAsync_SpanIdOf4Bytes_ThrowsInvalidDataNamingSpanId()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = TracesRequest(Span(TraceIdBytes, new byte[4]));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => receiver.ReceiveTracesAsync(payload).AsTask());

        Assert.Contains("span_id", ex.InnerException!.Message);
        Assert.Empty(store.SpanBatches);
    }

    [Fact]
    public async Task ReceiveTracesAsync_EndBeforeStart_ClampsDurationToZero()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        var span = new ProtobufWriter();
        span.WriteBytes(1, TraceIdBytes);
        span.WriteBytes(2, SpanIdBytes);
        span.WriteFixed64(7, EndUnixNanos);
        span.WriteFixed64(8, StartUnixNanos);

        await receiver.ReceiveTracesAsync(TracesRequest(span));

        SpanRecord record = Assert.Single(Assert.Single(store.SpanBatches));
        Assert.Equal(TimeSpan.Zero, record.Duration);
    }

    [Fact]
    public async Task ReceiveLogsAsync_SeverityAboveRange_ClampsTo24()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        var logRecord = new ProtobufWriter();
        logRecord.WriteFixed64(1, StartUnixNanos);
        logRecord.WriteVarint(2, 99);

        await receiver.ReceiveLogsAsync(LogsRequest(logRecord));

        LogRecord record = Assert.Single(Assert.Single(store.LogBatches));
        Assert.Equal(24, record.Severity);
    }

    [Fact]
    public void ValidateDepth_AtLimit_DoesNotThrow()
    {
        ProtobufReader.ValidateDepth(ProtobufReader.MaxMessageDepth);
    }

    [Fact]
    public void ValidateDepth_BeyondLimit_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => ProtobufReader.ValidateDepth(ProtobufReader.MaxMessageDepth + 1));
    }

    private static ProtobufWriter Span(byte[] traceId, byte[] spanId)
    {
        var span = new ProtobufWriter();
        span.WriteBytes(1, traceId);
        span.WriteBytes(2, spanId);
        span.WriteString(5, "POST /checkout");
        span.WriteFixed64(7, StartUnixNanos);
        span.WriteFixed64(8, EndUnixNanos);
        return span;
    }

    // ExportTraceServiceRequest { resource_spans(1) { scope_spans(2) { spans(2) } } }
    private static byte[] TracesRequest(params ProtobufWriter[] spans)
    {
        var scopeSpans = new ProtobufWriter();
        foreach (ProtobufWriter span in spans)
        {
            scopeSpans.WriteMessage(2, span);
        }

        var resourceSpans = new ProtobufWriter();
        resourceSpans.WriteMessage(2, scopeSpans);

        var request = new ProtobufWriter();
        request.WriteMessage(1, resourceSpans);
        return request.ToArray();
    }

    // ExportLogsServiceRequest { resource_logs(1) { scope_logs(2) { log_records(2) } } }
    private static byte[] LogsRequest(ProtobufWriter logRecord)
    {
        var scopeLogs = new ProtobufWriter();
        scopeLogs.WriteMessage(2, logRecord);

        var resourceLogs = new ProtobufWriter();
        resourceLogs.WriteMessage(2, scopeLogs);

        var request = new ProtobufWriter();
        request.WriteMessage(1, resourceLogs);
        return request.ToArray();
    }
}
