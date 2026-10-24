namespace DotnetOtelDashboard.Tests;

public class TraceWaterfallTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SpanRecord Span(string id, string? parent, double startMs, double durationMs) =>
        new("0af7651916cd43dd8448eb211c80319c", id, parent, "svc", id, SpanKind.Internal,
            Start.AddMilliseconds(startMs), TimeSpan.FromMilliseconds(durationMs), SpanStatusCode.Unset,
            new Dictionary<string, string>());

    private static void AssertPercents(double[] expected, IEnumerable<double> actual)
    {
        var values = actual.ToArray();
        Assert.Equal(expected.Length, values.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], values[i], precision: 3);
        }
    }

    [Fact]
    public void Build_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TraceWaterfall.Build(null!));
    }

    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(TraceWaterfall.Build([]));
    }

    [Fact]
    public void Build_OrdersDepthFirstByStartTime()
    {
        var spans = new[]
        {
            Span("c2", "root", 50, 10),
            Span("root", null, 0, 100),
            Span("c1", "root", 10, 30),
            Span("g1", "c1", 15, 5),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.Equal(["root", "c1", "g1", "c2"], rows.Select(r => r.Span.SpanId));
        Assert.Equal([0, 1, 2, 1], rows.Select(r => r.Depth));
    }

    [Fact]
    public void Build_ComputesOffsetAndWidthPercentages()
    {
        var spans = new[]
        {
            Span("root", null, 0, 200),
            Span("child", "root", 50, 100),
        };

        var rows = TraceWaterfall.Build(spans);

        var child = rows[1];
        Assert.Equal(TimeSpan.FromMilliseconds(50), child.Offset);
        Assert.Equal(25, child.OffsetPercent, precision: 6);
        Assert.Equal(50, child.WidthPercent, precision: 6);
    }

    [Fact]
    public void Build_OrphanSpanBecomesRoot()
    {
        var spans = new[]
        {
            Span("a", "missing-parent", 0, 10),
            Span("b", null, 5, 10),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.All(rows, r => Assert.Equal(0, r.Depth));
        Assert.Equal(["a", "b"], rows.Select(r => r.Span.SpanId));
    }

    [Fact]
    public void Build_SingleRoot_HasZeroOffsetAndFullWidth()
    {
        var rows = TraceWaterfall.Build([Span("root", null, 0, 100)]);

        var row = Assert.Single(rows);
        Assert.Equal(0, row.Depth);
        Assert.Equal(TimeSpan.Zero, row.Offset);
        Assert.Equal(0, row.OffsetPercent, precision: 6);
        Assert.Equal(100, row.WidthPercent, precision: 6);
    }

    [Fact]
    public void Build_ChildrenGivenInReverseOrder_ReturnsPreOrderWithPercentages()
    {
        var spans = new[]
        {
            Span("child2", "root", 50, 10),
            Span("root", null, 0, 100),
            Span("child1", "root", 10, 20),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.Equal(["root", "child1", "child2"], rows.Select(r => r.Span.SpanId));
        Assert.Equal([0, 1, 1], rows.Select(r => r.Depth));
        AssertPercents([0, 10, 50], rows.Select(r => r.OffsetPercent));
        AssertPercents([100, 20, 10], rows.Select(r => r.WidthPercent));
    }

    [Fact]
    public void Build_SpansWithIdenticalStartTime_OrderedBySpanId()
    {
        var spans = new[]
        {
            Span("root", null, 0, 100),
            Span("zeta", "root", 10, 5),
            Span("alpha", "root", 10, 5),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.Equal(["root", "alpha", "zeta"], rows.Select(r => r.Span.SpanId));
    }

    [Fact]
    public void Build_ZeroDurationSpan_WidthAtLeastMinimum()
    {
        var spans = new[]
        {
            Span("root", null, 0, 100),
            Span("marker", "root", 50, 0),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.True(rows[1].WidthPercent >= 0.1, $"WidthPercent was {rows[1].WidthPercent}");
    }

    [Fact]
    public void Build_TwoSpanCycle_ReturnsEachSpanExactlyOnce()
    {
        var spans = new[]
        {
            Span("a", "b", 0, 10),
            Span("b", "a", 10, 10),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.Equal(["a", "b"], rows.Select(r => r.Span.SpanId));
        Assert.Equal([0, 1], rows.Select(r => r.Depth));
    }

    [Fact]
    public void Build_CycleBelowRoot_TerminatesAndReturnsAllSpans()
    {
        var spans = new[]
        {
            Span("root", null, 0, 100),
            Span("x", "y", 10, 10),
            Span("y", "x", 20, 10),
        };

        var rows = TraceWaterfall.Build(spans);

        Assert.Equal(["root", "x", "y"], rows.Select(r => r.Span.SpanId));
        Assert.Equal([0, 0, 1], rows.Select(r => r.Depth));
    }

    [Fact]
    public void Build_ZeroLengthTrace_UsesMinimumWidth()
    {
        var spans = new[] { Span("only", null, 0, 0) };

        var rows = TraceWaterfall.Build(spans);

        Assert.Single(rows);
        Assert.Equal(0.1, rows[0].WidthPercent, precision: 6);
        Assert.Equal(0, rows[0].OffsetPercent);
    }
}
