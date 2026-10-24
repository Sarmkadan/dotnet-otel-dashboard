using DotnetOtelDashboard.Tests.Otlp;

namespace DotnetOtelDashboard.Tests;

public class OtlpTelemetryReceiverTests
{
    private static readonly byte[] TraceIdBytes = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];
    private const string TraceIdHex = "00000000000000000000000000000001";

    private static readonly byte[] FirstSpanIdBytes = [0, 0, 0, 0, 0, 0, 0, 1];
    private static readonly byte[] SecondSpanIdBytes = [0, 0, 0, 0, 0, 0, 0, 2];

    private const ulong StartUnixNanos = 1_700_000_000_000_000_000UL;
    private const ulong EndUnixNanos = StartUnixNanos + 250_000_000UL;

    [Fact]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OtlpTelemetryReceiver(null!));

        Assert.Equal("store", ex.ParamName);
    }

    [Fact]
    public async Task ReceiveTracesAsync_TwoSpans_ReturnsTwoAndStoresThemInOneBatch()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = TracesRequest(
            "checkout",
            Span(FirstSpanIdBytes, "POST /checkout"),
            Span(SecondSpanIdBytes, "SELECT orders"));

        int accepted = await receiver.ReceiveTracesAsync(payload);

        Assert.Equal(2, accepted);
        IReadOnlyList<SpanRecord> batch = Assert.Single(store.SpanBatches);
        Assert.Equal(2, batch.Count);

        Assert.Equal(TraceIdHex, batch[0].TraceId);
        Assert.Equal("0000000000000001", batch[0].SpanId);
        Assert.Equal("POST /checkout", batch[0].Name);
        Assert.Equal("checkout", batch[0].ServiceName);
        Assert.Equal(TimeSpan.FromMilliseconds(250), batch[0].Duration);

        Assert.Equal(TraceIdHex, batch[1].TraceId);
        Assert.Equal("0000000000000002", batch[1].SpanId);
        Assert.Equal("SELECT orders", batch[1].Name);
        Assert.Equal("checkout", batch[1].ServiceName);
        Assert.Equal(TimeSpan.FromMilliseconds(250), batch[1].Duration);

        Assert.Empty(store.MetricBatches);
        Assert.Empty(store.LogBatches);
    }

    [Fact]
    public async Task ReceiveMetricsAsync_OneGaugePoint_ReturnsOneAndStoresGauge()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = GaugeMetricsRequest("worker", metricName: "queue.depth", value: 42.5);

        int accepted = await receiver.ReceiveMetricsAsync(payload);

        Assert.Equal(1, accepted);
        IReadOnlyList<MetricPoint> batch = Assert.Single(store.MetricBatches);
        MetricPoint point = Assert.Single(batch);
        Assert.Equal(MetricType.Gauge, point.Type);
        Assert.Equal("queue.depth", point.MetricName);
        Assert.Equal("worker", point.ServiceName);
        Assert.Equal(42.5, point.Value);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks((long)(StartUnixNanos / 100)), point.Timestamp);

        Assert.Empty(store.SpanBatches);
        Assert.Empty(store.LogBatches);
    }

    [Fact]
    public async Task ReceiveLogsAsync_OneRecord_ReturnsOneAndStoresSeverity()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        byte[] payload = LogsRequest("payments", severityNumber: 17, severityText: "Error", body: "payment declined");

        int accepted = await receiver.ReceiveLogsAsync(payload);

        Assert.Equal(1, accepted);
        IReadOnlyList<LogRecord> batch = Assert.Single(store.LogBatches);
        LogRecord record = Assert.Single(batch);
        Assert.Equal(17, record.Severity);
        Assert.Equal("Error", record.SeverityText);
        Assert.Equal("payment declined", record.Body);
        Assert.Equal("payments", record.ServiceName);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks((long)(StartUnixNanos / 100)), record.Timestamp);

        Assert.Empty(store.SpanBatches);
        Assert.Empty(store.MetricBatches);
    }

    [Fact]
    public async Task ReceiveAsync_EmptyPayload_ReturnsZeroAndNeverWrites()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);

        int traces = await receiver.ReceiveTracesAsync(ReadOnlyMemory<byte>.Empty);
        int metrics = await receiver.ReceiveMetricsAsync(ReadOnlyMemory<byte>.Empty);
        int logs = await receiver.ReceiveLogsAsync(ReadOnlyMemory<byte>.Empty);

        Assert.Equal(0, traces);
        Assert.Equal(0, metrics);
        Assert.Equal(0, logs);
        Assert.Empty(store.SpanBatches);
        Assert.Empty(store.MetricBatches);
        Assert.Empty(store.LogBatches);
    }

    [Fact]
    public async Task ReceiveAsync_GarbageBytes_ThrowsInvalidDataExceptionAndNeverWrites()
    {
        var store = new RecordingTelemetryStore();
        var receiver = new OtlpTelemetryReceiver(store);
        // Eleven continuation bytes form a varint longer than the 10-byte protobuf limit.
        byte[] garbage = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        await Assert.ThrowsAsync<InvalidDataException>(() => receiver.ReceiveTracesAsync(garbage).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(() => receiver.ReceiveMetricsAsync(garbage).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(() => receiver.ReceiveLogsAsync(garbage).AsTask());

        Assert.Empty(store.SpanBatches);
        Assert.Empty(store.MetricBatches);
        Assert.Empty(store.LogBatches);
    }

    // Span { trace_id(1), span_id(2), name(5), start_time_unix_nano(7), end_time_unix_nano(8) }
    private static ProtobufWriter Span(byte[] spanId, string name)
    {
        var span = new ProtobufWriter();
        span.WriteBytes(1, TraceIdBytes);
        span.WriteBytes(2, spanId);
        span.WriteString(5, name);
        span.WriteFixed64(7, StartUnixNanos);
        span.WriteFixed64(8, EndUnixNanos);
        return span;
    }

    // ExportTraceServiceRequest { resource_spans(1) { resource(1), scope_spans(2) { spans(2) } } }
    private static byte[] TracesRequest(string serviceName, params ProtobufWriter[] spans)
    {
        var scopeSpans = new ProtobufWriter();
        foreach (ProtobufWriter span in spans)
        {
            scopeSpans.WriteMessage(2, span);
        }

        var resourceSpans = new ProtobufWriter();
        resourceSpans.WriteMessage(1, Resource(serviceName));
        resourceSpans.WriteMessage(2, scopeSpans);

        var request = new ProtobufWriter();
        request.WriteMessage(1, resourceSpans);
        return request.ToArray();
    }

    // ExportMetricsServiceRequest { resource_metrics(1) { resource(1), scope_metrics(2) { metrics(2) } } }
    // Metric { name(1), gauge(5) { data_points(1) { time_unix_nano(3), as_double(4) } } }
    private static byte[] GaugeMetricsRequest(string serviceName, string metricName, double value)
    {
        var dataPoint = new ProtobufWriter();
        dataPoint.WriteFixed64(3, StartUnixNanos);
        dataPoint.WriteDouble(4, value);

        var gauge = new ProtobufWriter();
        gauge.WriteMessage(1, dataPoint);

        var metric = new ProtobufWriter();
        metric.WriteString(1, metricName);
        metric.WriteMessage(5, gauge);

        var scopeMetrics = new ProtobufWriter();
        scopeMetrics.WriteMessage(2, metric);

        var resourceMetrics = new ProtobufWriter();
        resourceMetrics.WriteMessage(1, Resource(serviceName));
        resourceMetrics.WriteMessage(2, scopeMetrics);

        var request = new ProtobufWriter();
        request.WriteMessage(1, resourceMetrics);
        return request.ToArray();
    }

    // ExportLogsServiceRequest { resource_logs(1) { resource(1), scope_logs(2) { log_records(2) } } }
    // LogRecord { time_unix_nano(1), severity_number(2), severity_text(3), body(5) }
    private static byte[] LogsRequest(string serviceName, int severityNumber, string severityText, string body)
    {
        var logRecord = new ProtobufWriter();
        logRecord.WriteFixed64(1, StartUnixNanos);
        logRecord.WriteVarint(2, (ulong)severityNumber);
        logRecord.WriteString(3, severityText);
        logRecord.WriteMessage(5, AnyValue(body));

        var scopeLogs = new ProtobufWriter();
        scopeLogs.WriteMessage(2, logRecord);

        var resourceLogs = new ProtobufWriter();
        resourceLogs.WriteMessage(1, Resource(serviceName));
        resourceLogs.WriteMessage(2, scopeLogs);

        var request = new ProtobufWriter();
        request.WriteMessage(1, resourceLogs);
        return request.ToArray();
    }

    // Resource { attributes(1) { KeyValue { key(1), value(2) } } } with service.name set.
    private static ProtobufWriter Resource(string serviceName)
    {
        var keyValue = new ProtobufWriter();
        keyValue.WriteString(1, "service.name");
        keyValue.WriteMessage(2, AnyValue(serviceName));

        var resource = new ProtobufWriter();
        resource.WriteMessage(1, keyValue);
        return resource;
    }

    // AnyValue { string_value(1) }
    private static ProtobufWriter AnyValue(string value)
    {
        var anyValue = new ProtobufWriter();
        anyValue.WriteString(1, value);
        return anyValue;
    }
}
