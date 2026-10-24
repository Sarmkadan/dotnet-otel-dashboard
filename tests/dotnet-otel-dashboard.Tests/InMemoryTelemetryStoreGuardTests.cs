namespace DotnetOtelDashboard.Tests;

public class InMemoryTelemetryStoreGuardTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("options", () => new InMemoryTelemetryStore(null!));
    }

    [Theory]
    [InlineData(nameof(OtelDashboardOptions.MaxSpans))]
    [InlineData(nameof(OtelDashboardOptions.MaxMetricPoints))]
    [InlineData(nameof(OtelDashboardOptions.MaxLogRecords))]
    public void Constructor_NonPositiveLimit_ThrowsArgumentOutOfRangeException(string propertyName)
    {
        var options = new OtelDashboardOptions();
        typeof(OtelDashboardOptions).GetProperty(propertyName)!.SetValue(options, 0);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryTelemetryStore(options));

        Assert.Equal(propertyName, ex.ParamName);
    }

    [Fact]
    public void Constructor_NonPositiveRetention_ThrowsArgumentOutOfRangeException()
    {
        var options = new OtelDashboardOptions { Retention = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryTelemetryStore(options));
    }

    [Fact]
    public void WriteSpansAsync_NullSpans_ThrowsSynchronously()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("spans", () => store.WriteSpansAsync(null!));
    }

    [Fact]
    public void WriteMetricsAsync_NullPoints_ThrowsSynchronously()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("points", () => store.WriteMetricsAsync(null!));
    }

    [Fact]
    public void WriteLogsAsync_NullRecords_ThrowsSynchronously()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("records", () => store.WriteLogsAsync(null!));
    }

    [Fact]
    public void QuerySpansAsync_NullQuery_ThrowsSynchronously()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("query", () => store.QuerySpansAsync(null!));
    }

    [Fact]
    public void QueryMetricsAsync_NullQuery_ThrowsSynchronously()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("query", () => store.QueryMetricsAsync(null!));
    }

    [Fact]
    public void QueryLogsAsync_NullQuery_ThrowsSynchronously()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("query", () => store.QueryLogsAsync(null!));
    }

    [Fact]
    public void GetTraceAsync_NullTraceId_ThrowsArgumentNullException()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>("traceId", () => store.GetTraceAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void GetTraceAsync_EmptyOrWhitespaceTraceId_ThrowsArgumentException(string traceId)
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>("traceId", () => store.GetTraceAsync(traceId));
    }

    private static InMemoryTelemetryStore CreateStore()
    {
        return new InMemoryTelemetryStore(new OtelDashboardOptions(), new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Start));
    }
}
