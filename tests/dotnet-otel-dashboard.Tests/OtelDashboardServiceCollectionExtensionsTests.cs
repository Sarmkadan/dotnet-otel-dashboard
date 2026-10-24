using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotnetOtelDashboard.Tests;

public class OtelDashboardServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOtelDashboard_NullServices_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddOtelDashboard());

        Assert.Equal("services", ex.ParamName);
    }

    [Fact]
    public void AddOtelDashboard_ITelemetryStore_ResolvesAsInMemoryTelemetryStore()
    {
        var services = new ServiceCollection().AddOtelDashboard();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ITelemetryStore>();

        Assert.IsType<InMemoryTelemetryStore>(store);
    }

    [Fact]
    public void AddOtelDashboard_ITelemetryReceiver_ResolvesAsOtlpTelemetryReceiver()
    {
        var services = new ServiceCollection().AddOtelDashboard();
        using var provider = services.BuildServiceProvider();

        var receiver = provider.GetRequiredService<ITelemetryReceiver>();

        Assert.IsType<OtlpTelemetryReceiver>(receiver);
    }

    [Fact]
    public void AddOtelDashboard_ConfigureCallback_IsApplied()
    {
        var services = new ServiceCollection().AddOtelDashboard(o => o.MaxSpans = 42);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OtelDashboardOptions>>().Value;

        Assert.Equal(42, options.MaxSpans);
    }

    [Fact]
    public void AddOtelDashboard_WithoutConfigureCallback_UsesDefaultOptions()
    {
        var services = new ServiceCollection().AddOtelDashboard();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OtelDashboardOptions>>().Value;

        Assert.Equal(new OtelDashboardOptions().MaxSpans, options.MaxSpans);
    }

    [Fact]
    public void AddOtelDashboard_PreRegisteredStore_IsNotReplaced()
    {
        var custom = new RecordingTelemetryStore();
        var services = new ServiceCollection();
        services.AddSingleton<ITelemetryStore>(custom);

        services.AddOtelDashboard();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ITelemetryStore>();

        Assert.Same(custom, store);
    }

    [Fact]
    public void AddOtelDashboard_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddOtelDashboard();

        Assert.Same(services, result);
    }
}
