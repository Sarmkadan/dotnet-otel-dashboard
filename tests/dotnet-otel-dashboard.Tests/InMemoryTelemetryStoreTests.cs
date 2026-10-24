using Microsoft.Extensions.Time.Testing;

namespace DotnetOtelDashboard.Tests;

public class InMemoryTelemetryStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetTraceAsync_ReturnsSpansOfTraceOrderedByStartTime()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteSpansAsync(
        [
            Span("trace-a", "span-3", "span-1", "api", "GET /", Start.AddSeconds(2), TimeSpan.FromMilliseconds(5)),
            Span("trace-a", "span-1", null, "api", "GET /", Start, TimeSpan.FromMilliseconds(50)),
            Span("trace-b", "span-9", null, "api", "GET /", Start.AddSeconds(1), TimeSpan.FromMilliseconds(1)),
            Span("trace-a", "span-2", "span-1", "db", "SELECT", Start.AddSeconds(1), TimeSpan.FromMilliseconds(10)),
        ]);

        var trace = await store.GetTraceAsync("trace-a");

        Assert.Equal(["span-1", "span-2", "span-3"], trace.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task GetTraceAsync_UnknownTrace_ReturnsEmptyList()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteSpansAsync([Span("trace-a", "span-1", null, "api", "GET /", Start, TimeSpan.FromMilliseconds(5))]);

        var trace = await store.GetTraceAsync("trace-missing");

        Assert.Empty(trace);
    }

    [Fact]
    public async Task QuerySpansAsync_FiltersByServiceName()
    {
        var store = await SeedDatasetAsync();

        var result = await store.QuerySpansAsync(new SpanQuery(ServiceName: "checkout"));

        Assert.Equal(["b1", "a1"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_FiltersBySpanName()
    {
        var store = await SeedDatasetAsync();

        var result = await store.QuerySpansAsync(new SpanQuery(SpanName: "charge card"));

        Assert.Equal(["b2", "a2"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_MinDuration_IsInclusive()
    {
        var store = await SeedDatasetAsync();

        var result = await store.QuerySpansAsync(new SpanQuery(MinDuration: TimeSpan.FromMilliseconds(40)));

        Assert.Equal(["b2", "a2", "a1"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_ErrorsOnly_ReturnsOnlyErrorSpans()
    {
        var store = await SeedDatasetAsync();

        var result = await store.QuerySpansAsync(new SpanQuery(ErrorsOnly: true));

        Assert.Equal(["b2", "a2"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_From_IsInclusiveAndTo_IsExclusive()
    {
        var store = await SeedDatasetAsync();

        // From = Start+1s includes the span starting exactly at 1s; To = Start+3s excludes the span starting at 3s.
        var result = await store.QuerySpansAsync(new SpanQuery(From: Start.AddSeconds(1), To: Start.AddSeconds(3)));

        Assert.Equal(["b1", "a2"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_ReturnsNewestFirst()
    {
        var store = await SeedDatasetAsync();

        var result = await store.QuerySpansAsync(new SpanQuery());

        Assert.Equal(["c1", "b2", "b1", "a2", "a1"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_RespectsLimit()
    {
        var store = await SeedDatasetAsync();

        var result = await store.QuerySpansAsync(new SpanQuery(Limit: 2));

        Assert.Equal(["c1", "b2"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task WriteSpansAsync_AboveMaxSpans_KeepsThreeNewest()
    {
        var store = CreateStore(new FakeTimeProvider(Start), new OtelDashboardOptions { MaxSpans = 3 });

        for (var i = 1; i <= 5; i++)
        {
            await store.WriteSpansAsync([Span("trace-a", $"span-{i}", null, "api", "GET /", Start.AddSeconds(i), TimeSpan.FromMilliseconds(1))]);
        }

        var result = await store.QuerySpansAsync(new SpanQuery());

        Assert.Equal(["span-5", "span-4", "span-3"], result.Select(span => span.SpanId).ToArray());
    }

    [Fact]
    public async Task QuerySpansAsync_SpansOlderThanRetention_AreEvicted()
    {
        var time = new FakeTimeProvider(Start);
        var store = CreateStore(time, new OtelDashboardOptions { Retention = TimeSpan.FromMinutes(10) });
        await store.WriteSpansAsync([Span("trace-a", "span-1", null, "api", "GET /", Start, TimeSpan.FromMilliseconds(1))]);

        time.Advance(TimeSpan.FromMinutes(11));
        var result = await store.QuerySpansAsync(new SpanQuery());

        Assert.Empty(result);
    }

    // Seeds five spans across three traces, starting one second apart:
    //   a1 checkout  POST /pay      0s  120ms Ok
    //   a2 payments  charge card    1s  900ms Error (child of a1)
    //   b1 checkout  GET /cart      2s   15ms Unset
    //   b2 catalog   charge card    3s   40ms Error (child of b1)
    //   c1 catalog   GET /items     4s    5ms Ok
    private static async Task<InMemoryTelemetryStore> SeedDatasetAsync()
    {
        var store = CreateStore(new FakeTimeProvider(Start));
        await store.WriteSpansAsync(
        [
            Span("trace-1", "a1", null, "checkout", "POST /pay", Start, TimeSpan.FromMilliseconds(120), SpanStatusCode.Ok),
            Span("trace-1", "a2", "a1", "payments", "charge card", Start.AddSeconds(1), TimeSpan.FromMilliseconds(900), SpanStatusCode.Error),
            Span("trace-2", "b1", null, "checkout", "GET /cart", Start.AddSeconds(2), TimeSpan.FromMilliseconds(15)),
            Span("trace-2", "b2", "b1", "catalog", "charge card", Start.AddSeconds(3), TimeSpan.FromMilliseconds(40), SpanStatusCode.Error),
            Span("trace-3", "c1", null, "catalog", "GET /items", Start.AddSeconds(4), TimeSpan.FromMilliseconds(5), SpanStatusCode.Ok),
        ]);

        return store;
    }

    private static InMemoryTelemetryStore CreateStore(TimeProvider timeProvider, OtelDashboardOptions? options = null)
    {
        return new InMemoryTelemetryStore(options ?? new OtelDashboardOptions(), timeProvider);
    }

    private static SpanRecord Span(
        string traceId,
        string spanId,
        string? parent,
        string service,
        string name,
        DateTimeOffset start,
        TimeSpan duration,
        SpanStatusCode status = SpanStatusCode.Unset)
    {
        return new SpanRecord(
            traceId,
            spanId,
            parent,
            service,
            name,
            SpanKind.Internal,
            start,
            duration,
            status,
            new Dictionary<string, string>());
    }
}
