namespace DotnetOtelDashboard.Tests;

public class OtelDashboardBuilderTests
{
    [Fact]
    public void Build_WithoutConfiguration_UsesDefaults()
    {
        var dashboard = new OtelDashboardBuilder().Build();

        Assert.IsType<InMemoryTelemetryStore>(dashboard.Store);
        Assert.IsType<OtlpTelemetryReceiver>(dashboard.Receiver);
        Assert.Equal(new OtelDashboardOptions().MaxSpans, dashboard.Options.MaxSpans);
        Assert.Equal(new OtelDashboardOptions().MaxMetricPoints, dashboard.Options.MaxMetricPoints);
        Assert.Equal(new OtelDashboardOptions().MaxLogRecords, dashboard.Options.MaxLogRecords);
        Assert.Equal(new OtelDashboardOptions().Retention, dashboard.Options.Retention);
        Assert.Equal("/", dashboard.Options.ReceiverBasePath);
        Assert.True(dashboard.Options.EnableGrpcReceiver);
        Assert.Null(dashboard.Options.AuthorizationPolicy);
    }

    [Fact]
    public void Configure_AppliesCallbackToOptions()
    {
        var dashboard = new OtelDashboardBuilder()
            .Configure(o => o.MaxSpans = 10)
            .Build();

        Assert.Equal(10, dashboard.Options.MaxSpans);
    }

    [Fact]
    public void Configure_CalledTwice_LaterWinsForSameProperty()
    {
        var dashboard = new OtelDashboardBuilder()
            .Configure(o => o.MaxSpans = 10)
            .Configure(o => o.MaxSpans = 20)
            .Build();

        Assert.Equal(20, dashboard.Options.MaxSpans);
    }

    [Fact]
    public void Configure_CalledTwice_BothApplyForDifferentProperties()
    {
        var dashboard = new OtelDashboardBuilder()
            .Configure(o => o.MaxSpans = 10)
            .Configure(o => o.MaxLogRecords = 30)
            .Build();

        Assert.Equal(10, dashboard.Options.MaxSpans);
        Assert.Equal(30, dashboard.Options.MaxLogRecords);
    }

    [Fact]
    public void Configure_Null_Throws()
    {
        var builder = new OtelDashboardBuilder();

        var ex = Assert.Throws<ArgumentNullException>(() => builder.Configure(null!));

        Assert.Equal("configure", ex.ParamName);
    }

    [Fact]
    public void UseStore_Null_Throws()
    {
        var builder = new OtelDashboardBuilder();

        var ex = Assert.Throws<ArgumentNullException>(() => builder.UseStore(null!));

        Assert.Equal("factory", ex.ParamName);
    }

    [Fact]
    public void UseStore_FactoryReceivesValidatedOptions()
    {
        OtelDashboardOptions? received = null;
        var builder = new OtelDashboardBuilder()
            .Configure(o => o.ReceiverBasePath = "/x/")
            .UseStore(options =>
            {
                received = options;
                return new InMemoryTelemetryStore(options);
            });

        var dashboard = builder.Build();

        Assert.NotNull(received);
        Assert.Equal("/x", received.ReceiverBasePath);
        Assert.Equal("/x", dashboard.Options.ReceiverBasePath);
    }

    [Fact]
    public void UseStore_BuiltDashboardUsesFactoryStore()
    {
        ITelemetryStore? created = null;
        var dashboard = new OtelDashboardBuilder()
            .UseStore(options =>
            {
                created = new InMemoryTelemetryStore(options);
                return created;
            })
            .Build();

        Assert.Same(created, dashboard.Store);
    }

    [Fact]
    public void UseStore_FactoryReturnsNull_BuildThrowsInvalidOperationException()
    {
        var builder = new OtelDashboardBuilder().UseStore(_ => null!);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_InvalidMaxSpans_ThrowsArgumentOutOfRange()
    {
        var builder = new OtelDashboardBuilder().Configure(o => o.MaxSpans = 0);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());

        Assert.Equal(nameof(OtelDashboardOptions.MaxSpans), ex.ParamName);
    }

    [Fact]
    public void Build_InvalidReceiverBasePath_ThrowsArgumentException()
    {
        var builder = new OtelDashboardBuilder().Configure(o => o.ReceiverBasePath = "bad");

        var ex = Assert.Throws<ArgumentException>(() => builder.Build());

        Assert.Equal(nameof(OtelDashboardOptions.ReceiverBasePath), ex.ParamName);
    }

    [Fact]
    public void Build_CalledRepeatedly_ReturnsFreshStoreEachTime()
    {
        var builder = new OtelDashboardBuilder();

        var first = builder.Build();
        var second = builder.Build();

        Assert.IsType<InMemoryTelemetryStore>(first.Store);
        Assert.IsType<InMemoryTelemetryStore>(second.Store);
        Assert.NotSame(first.Store, second.Store);
    }
}
