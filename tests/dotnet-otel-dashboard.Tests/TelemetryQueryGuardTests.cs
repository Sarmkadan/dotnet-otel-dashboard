namespace DotnetOtelDashboard.Tests;

public class TelemetryQueryGuardTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SpanQuery_NonPositiveLimit_ThrowsArgumentOutOfRangeException(int limit)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SpanQuery(Limit: limit));

        Assert.Equal("Limit", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LogQuery_NonPositiveLimit_ThrowsArgumentOutOfRangeException(int limit)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LogQuery(Limit: limit));

        Assert.Equal("Limit", ex.ParamName);
    }

    [Fact]
    public void SpanQuery_InvertedRange_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SpanQuery(From: Start.AddMinutes(2), To: Start.AddMinutes(1)));

        Assert.Equal("To", ex.ParamName);
    }

    [Fact]
    public void LogQuery_InvertedRange_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new LogQuery(From: Start.AddMinutes(2), To: Start.AddMinutes(1)));

        Assert.Equal("To", ex.ParamName);
    }

    [Fact]
    public void MetricQuery_InvertedRange_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new MetricQuery("cpu.usage", null, Start.AddMinutes(2), Start.AddMinutes(1)));

        Assert.Equal("To", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void MetricQuery_WhitespaceMetricName_ThrowsArgumentException(string metricName)
    {
        var ex = Assert.Throws<ArgumentException>(() => new MetricQuery(metricName, null, Start, Start.AddMinutes(1)));

        Assert.Equal("MetricName", ex.ParamName);
    }

    [Fact]
    public void MetricQuery_NullMetricName_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MetricQuery(null!, null, Start, Start.AddMinutes(1)));
    }

    [Fact]
    public void SpanQuery_ValidArguments_SetsProperties()
    {
        var query = new SpanQuery("checkout", "charge card", TimeSpan.FromMilliseconds(40), true, Start, Start.AddMinutes(1), 25);

        Assert.Equal("checkout", query.ServiceName);
        Assert.Equal("charge card", query.SpanName);
        Assert.Equal(TimeSpan.FromMilliseconds(40), query.MinDuration);
        Assert.True(query.ErrorsOnly);
        Assert.Equal(Start, query.From);
        Assert.Equal(Start.AddMinutes(1), query.To);
        Assert.Equal(25, query.Limit);
    }

    [Fact]
    public void SpanQuery_Defaults_AreUnchanged()
    {
        var query = new SpanQuery();

        Assert.Null(query.ServiceName);
        Assert.Null(query.SpanName);
        Assert.Null(query.MinDuration);
        Assert.False(query.ErrorsOnly);
        Assert.Null(query.From);
        Assert.Null(query.To);
        Assert.Equal(100, query.Limit);
    }

    [Fact]
    public void SpanQuery_EqualBounds_AreAllowed()
    {
        var query = new SpanQuery(From: Start, To: Start);

        Assert.Equal(Start, query.From);
        Assert.Equal(Start, query.To);
    }

    [Fact]
    public void MetricQuery_ValidArguments_SetsProperties()
    {
        var query = new MetricQuery("http.server.duration", "api", Start, Start.AddMinutes(1));

        Assert.Equal("http.server.duration", query.MetricName);
        Assert.Equal("api", query.ServiceName);
        Assert.Equal(Start, query.From);
        Assert.Equal(Start.AddMinutes(1), query.To);
    }

    [Fact]
    public void LogQuery_ValidArguments_SetsProperties()
    {
        var query = new LogQuery("api", 13, "trace-a", "ERROR", Start, Start.AddMinutes(1), 5);

        Assert.Equal("api", query.ServiceName);
        Assert.Equal(13, query.MinSeverity);
        Assert.Equal("trace-a", query.TraceId);
        Assert.Equal("ERROR", query.Search);
        Assert.Equal(Start, query.From);
        Assert.Equal(Start.AddMinutes(1), query.To);
        Assert.Equal(5, query.Limit);
    }

    [Fact]
    public void LogQuery_Defaults_AreUnchanged()
    {
        var query = new LogQuery();

        Assert.Null(query.ServiceName);
        Assert.Null(query.MinSeverity);
        Assert.Null(query.TraceId);
        Assert.Null(query.Search);
        Assert.Null(query.From);
        Assert.Null(query.To);
        Assert.Equal(100, query.Limit);
    }

    [Fact]
    public void SpanQuery_WithLimit_ReRunsValidation()
    {
        var query = new SpanQuery();

        var updated = query with { Limit = 5 };

        Assert.Equal(5, updated.Limit);
        Assert.Equal(100, query.Limit);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = query with { Limit = 0 }; });
    }

    [Fact]
    public void LogQuery_WithLimit_ReRunsValidation()
    {
        var query = new LogQuery();

        var updated = query with { Limit = 5 };

        Assert.Equal(5, updated.Limit);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = query with { Limit = -1 }; });
    }

    [Fact]
    public void SpanQuery_WithFrom_ReRunsRangeValidation()
    {
        var query = new SpanQuery(To: Start);

        var updated = query with { From = Start.AddMinutes(-1) };

        Assert.Equal(Start.AddMinutes(-1), updated.From);
        Assert.Throws<ArgumentException>(() => { _ = query with { From = Start.AddMinutes(1) }; });
    }

    [Fact]
    public void LogQuery_WithTo_ReRunsRangeValidation()
    {
        var query = new LogQuery(From: Start);

        var updated = query with { To = Start.AddMinutes(1) };

        Assert.Equal(Start.AddMinutes(1), updated.To);
        Assert.Throws<ArgumentException>(() => { _ = query with { To = Start.AddMinutes(-1) }; });
    }

    [Fact]
    public void MetricQuery_WithFrom_ReRunsRangeValidation()
    {
        var query = new MetricQuery("cpu.usage", null, Start, Start.AddMinutes(1));

        var updated = query with { From = Start.AddSeconds(30) };

        Assert.Equal(Start.AddSeconds(30), updated.From);
        Assert.Throws<ArgumentException>(() => { _ = query with { From = Start.AddMinutes(2) }; });
    }

    [Fact]
    public void MetricQuery_WithMetricName_ReRunsValidation()
    {
        var query = new MetricQuery("cpu.usage", null, Start, Start.AddMinutes(1));

        var updated = query with { MetricName = "memory.usage" };

        Assert.Equal("memory.usage", updated.MetricName);
        Assert.Throws<ArgumentException>(() => { _ = query with { MetricName = "  " }; });
    }
}
