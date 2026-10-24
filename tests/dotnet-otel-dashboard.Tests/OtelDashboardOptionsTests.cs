namespace DotnetOtelDashboard.Tests;

public class OtelDashboardOptionsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var options = new OtelDashboardOptions();

        options.Validate();

        Assert.Equal("/", options.ReceiverBasePath);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_NonPositiveMaxSpans_Throws(int value)
    {
        var options = new OtelDashboardOptions { MaxSpans = value };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(OtelDashboardOptions.MaxSpans), ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_NonPositiveMaxMetricPoints_Throws(int value)
    {
        var options = new OtelDashboardOptions { MaxMetricPoints = value };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(OtelDashboardOptions.MaxMetricPoints), ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_NonPositiveMaxLogRecords_Throws(int value)
    {
        var options = new OtelDashboardOptions { MaxLogRecords = value };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(OtelDashboardOptions.MaxLogRecords), ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveRetention_Throws(long ticks)
    {
        var options = new OtelDashboardOptions { Retention = TimeSpan.FromTicks(ticks) };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(OtelDashboardOptions.Retention), ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("otlp")]
    [InlineData("/a b")]
    [InlineData("/a?x")]
    [InlineData("/a#x")]
    public void Validate_InvalidReceiverBasePath_Throws(string path)
    {
        var options = new OtelDashboardOptions { ReceiverBasePath = path };

        var ex = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(OtelDashboardOptions.ReceiverBasePath), ex.ParamName);
    }

    [Fact]
    public void Validate_TrailingSlashInReceiverBasePath_IsTrimmed()
    {
        var options = new OtelDashboardOptions { ReceiverBasePath = "/otlp/" };

        options.Validate();

        Assert.Equal("/otlp", options.ReceiverBasePath);
    }

    [Fact]
    public void Validate_RootReceiverBasePath_IsKept()
    {
        var options = new OtelDashboardOptions { ReceiverBasePath = "/" };

        options.Validate();

        Assert.Equal("/", options.ReceiverBasePath);
    }
}
