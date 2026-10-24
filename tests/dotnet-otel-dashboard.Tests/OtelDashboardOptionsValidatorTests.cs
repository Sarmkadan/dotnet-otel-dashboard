namespace DotnetOtelDashboard.Tests;

public class OtelDashboardOptionsValidatorTests
{
    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        var validator = new OtelDashboardOptionsValidator();

        var result = validator.Validate(null, new OtelDashboardOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MaxSpansZero_Fails_WithMessageContainingMaxSpans()
    {
        var validator = new OtelDashboardOptionsValidator();

        var result = validator.Validate(null, new OtelDashboardOptions { MaxSpans = 0 });

        Assert.True(result.Failed);
        Assert.Contains("MaxSpans", result.FailureMessage);
    }

    [Fact]
    public void Validate_ReceiverBasePathWithoutLeadingSlash_Fails()
    {
        var validator = new OtelDashboardOptionsValidator();

        var result = validator.Validate(null, new OtelDashboardOptions { ReceiverBasePath = "nope" });

        Assert.True(result.Failed);
        Assert.Contains("ReceiverBasePath", result.FailureMessage);
    }
}
