using Microsoft.Extensions.Time.Testing;

namespace DotnetOtelDashboard.Tests;

public class InMemoryTelemetryStoreLogsMetricsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueryMetricsAsync_ReturnsOnlyRequestedMetricName()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteMetricsAsync(
        [
            Metric("api", "http.server.duration", Start, 12),
            Metric("api", "http.server.active", Start, 3),
            Metric("api", "http.server.duration", Start.AddSeconds(1), 20),
        ]);

        var result = await store.QueryMetricsAsync(new MetricQuery("http.server.duration", null, Start, Start.AddMinutes(1)));

        Assert.Equal([12d, 20d], result.Select(point => point.Value).ToArray());
        Assert.All(result, point => Assert.Equal("http.server.duration", point.MetricName));
    }

    [Fact]
    public async Task QueryMetricsAsync_FiltersByServiceName()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteMetricsAsync(
        [
            Metric("api", "queue.depth", Start, 1),
            Metric("worker", "queue.depth", Start.AddSeconds(1), 2),
            Metric("api", "queue.depth", Start.AddSeconds(2), 3),
        ]);

        var all = await store.QueryMetricsAsync(new MetricQuery("queue.depth", null, Start, Start.AddMinutes(1)));
        var apiOnly = await store.QueryMetricsAsync(new MetricQuery("queue.depth", "api", Start, Start.AddMinutes(1)));

        Assert.Equal(3, all.Count);
        Assert.Equal([1d, 3d], apiOnly.Select(point => point.Value).ToArray());
    }

    [Fact]
    public async Task QueryMetricsAsync_FromIsInclusiveAndToIsExclusive()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteMetricsAsync(
        [
            Metric("api", "cpu.usage", Start, 1),
            Metric("api", "cpu.usage", Start.AddSeconds(10), 2),
            Metric("api", "cpu.usage", Start.AddSeconds(20), 3),
        ]);

        // From lands exactly on the second point (included); To lands exactly on the third point (excluded).
        var result = await store.QueryMetricsAsync(new MetricQuery("cpu.usage", null, Start.AddSeconds(10), Start.AddSeconds(20)));

        Assert.Equal([2d], result.Select(point => point.Value).ToArray());
    }

    [Fact]
    public async Task QueryMetricsAsync_OrdersByTimestampAscending()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteMetricsAsync(
        [
            Metric("api", "requests", Start.AddSeconds(30), 3),
            Metric("api", "requests", Start, 1),
            Metric("api", "requests", Start.AddSeconds(15), 2),
        ]);

        var result = await store.QueryMetricsAsync(new MetricQuery("requests", null, Start, Start.AddMinutes(1)));

        Assert.Equal([1d, 2d, 3d], result.Select(point => point.Value).ToArray());
    }

    [Fact]
    public async Task QueryMetricsAsync_HistogramPoint_KeepsHistogramBuckets()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        var buckets = new Dictionary<double, long>
        {
            [5] = 2,
            [10] = 5,
            [double.PositiveInfinity] = 6,
        };
        await store.WriteMetricsAsync([Metric("api", "latency", Start, 42.5, buckets)]);

        var result = await store.QueryMetricsAsync(new MetricQuery("latency", null, Start, Start.AddMinutes(1)));

        var point = Assert.Single(result);
        Assert.Equal(MetricType.Histogram, point.Type);
        Assert.Equal(42.5, point.Value);
        Assert.NotNull(point.HistogramBuckets);
        Assert.Equal(buckets.Count, point.HistogramBuckets.Count);
        Assert.Equal(2, point.HistogramBuckets[5]);
        Assert.Equal(5, point.HistogramBuckets[10]);
        Assert.Equal(6, point.HistogramBuckets[double.PositiveInfinity]);
    }

    [Fact]
    public async Task QueryLogsAsync_MinSeverity_IsInclusive()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteLogsAsync(
        [
            Log("api", Start, 9, "info message"),
            Log("api", Start.AddSeconds(1), 13, "warning message"),
            Log("api", Start.AddSeconds(2), 17, "error message"),
            Log("api", Start.AddSeconds(3), 21, "fatal message"),
        ]);

        var result = await store.QueryLogsAsync(new LogQuery(MinSeverity: 13));

        Assert.Equal(["fatal message", "error message", "warning message"], result.Select(record => record.Body).ToArray());
    }

    [Fact]
    public async Task QueryLogsAsync_FiltersByTraceId()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteLogsAsync(
        [
            Log("api", Start, 9, "first", traceId: "trace-a"),
            Log("api", Start.AddSeconds(1), 9, "second", traceId: "trace-b"),
            Log("api", Start.AddSeconds(2), 9, "third", traceId: "trace-a"),
            Log("api", Start.AddSeconds(3), 9, "uncorrelated"),
        ]);

        var result = await store.QueryLogsAsync(new LogQuery(TraceId: "trace-a"));

        Assert.Equal(["third", "first"], result.Select(record => record.Body).ToArray());
    }

    [Fact]
    public async Task QueryLogsAsync_Search_IsCaseInsensitiveSubstringOnBody()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteLogsAsync(
        [
            Log("api", Start, 17, "an error occurred"),
            Log("api", Start.AddSeconds(1), 9, "all good"),
            Log("api", Start.AddSeconds(2), 17, "ERROR: disk full"),
            Log("api", Start.AddSeconds(3), 9, "terror alert"),
        ]);

        var result = await store.QueryLogsAsync(new LogQuery(Search: "ERROR"));

        Assert.Equal(["terror alert", "ERROR: disk full", "an error occurred"], result.Select(record => record.Body).ToArray());
    }

    [Fact]
    public async Task QueryLogsAsync_ReturnsNewestFirst()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteLogsAsync(
        [
            Log("api", Start.AddSeconds(2), 9, "middle"),
            Log("api", Start, 9, "oldest"),
            Log("api", Start.AddSeconds(4), 9, "newest"),
        ]);

        var result = await store.QueryLogsAsync(new LogQuery());

        Assert.Equal(["newest", "middle", "oldest"], result.Select(record => record.Body).ToArray());
    }

    [Fact]
    public async Task QueryLogsAsync_RespectsLimit()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        for (var i = 1; i <= 5; i++)
        {
            await store.WriteLogsAsync([Log("api", Start.AddSeconds(i), 9, $"log-{i}")]);
        }

        var result = await store.QueryLogsAsync(new LogQuery(Limit: 2));

        Assert.Equal(["log-5", "log-4"], result.Select(record => record.Body).ToArray());
    }

    [Fact]
    public async Task WriteLogsAsync_AboveMaxLogRecords_KeepsTwoNewest()
    {
        var store = CreateStore(new FakeTimeProvider(Start), new OtelDashboardOptions { MaxLogRecords = 2 });
        await store.WriteLogsAsync(
        [
            Log("api", Start, 9, "log-1"),
            Log("api", Start.AddSeconds(1), 9, "log-2"),
            Log("api", Start.AddSeconds(2), 9, "log-3"),
        ]);

        var result = await store.QueryLogsAsync(new LogQuery());

        Assert.Equal(["log-3", "log-2"], result.Select(record => record.Body).ToArray());
    }

    [Fact]
    public async Task GetServiceNamesAsync_ReturnsDistinctSortedNamesAcrossAllSignals()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteSpansAsync([Span("trace-1", "span-1", "checkout", Start)]);
        await store.WriteMetricsAsync([Metric("worker", "queue.depth", Start, 1), Metric("checkout", "queue.depth", Start, 2)]);
        await store.WriteLogsAsync([Log("gateway", Start, 9, "hello"), Log("worker", Start, 9, "again")]);

        var names = await store.GetServiceNamesAsync();

        Assert.Equal(["checkout", "gateway", "worker"], names.ToArray());
    }

    [Fact]
    public async Task ClearAsync_EmptiesSpansMetricsAndLogs()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteSpansAsync([Span("trace-1", "span-1", "checkout", Start)]);
        await store.WriteMetricsAsync([Metric("worker", "queue.depth", Start, 1)]);
        await store.WriteLogsAsync([Log("gateway", Start, 9, "hello")]);

        await store.ClearAsync();

        Assert.Empty(await store.QuerySpansAsync(new SpanQuery()));
        Assert.Empty(await store.QueryMetricsAsync(new MetricQuery("queue.depth", null, Start, Start.AddMinutes(1))));
        Assert.Empty(await store.QueryLogsAsync(new LogQuery()));
        Assert.Empty(await store.GetServiceNamesAsync());
    }

    private static InMemoryTelemetryStore CreateStore(TimeProvider timeProvider, OtelDashboardOptions? options = null)
    {
        return new InMemoryTelemetryStore(options ?? new OtelDashboardOptions(), timeProvider);
    }

    private static SpanRecord Span(string traceId, string spanId, string service, DateTimeOffset start)
    {
        return new SpanRecord(
            traceId,
            spanId,
            null,
            service,
            "GET /",
            SpanKind.Server,
            start,
            TimeSpan.FromMilliseconds(1),
            SpanStatusCode.Ok,
            new Dictionary<string, string>());
    }

    private static MetricPoint Metric(
        string service,
        string name,
        DateTimeOffset timestamp,
        double value,
        IReadOnlyDictionary<double, long>? buckets = null)
    {
        return new MetricPoint(
            service,
            name,
            "1",
            buckets is null ? MetricType.Gauge : MetricType.Histogram,
            timestamp,
            value,
            new Dictionary<string, string>(),
            buckets);
    }

    private static LogRecord Log(string service, DateTimeOffset timestamp, int severity, string body, string? traceId = null)
    {
        return new LogRecord(
            service,
            timestamp,
            severity,
            severity.ToString(),
            body,
            traceId,
            null,
            new Dictionary<string, string>());
    }
}
