using Microsoft.Extensions.Options;

namespace DotnetOtelDashboard;

/// <summary>
/// Validates <see cref="OtelDashboardOptions"/> through the Options pipeline, so invalid configuration
/// surfaces when the options are resolved (or at host start when <c>ValidateOnStart</c> is used)
/// rather than later at runtime. Delegates the rules to <see cref="OtelDashboardOptions.Validate"/>.
/// </summary>
public sealed class OtelDashboardOptionsValidator : IValidateOptions<OtelDashboardOptions>
{
    /// <summary>
    /// Validates the given options instance.
    /// </summary>
    /// <param name="name">The options instance name. Ignored: the dashboard uses the default (unnamed) instance.</param>
    /// <param name="options">The options to validate. Normalization performed by <see cref="OtelDashboardOptions.Validate"/> is applied in place.</param>
    /// <returns>
    /// <see cref="ValidateOptionsResult.Success"/> when the options are valid; otherwise a failed result
    /// carrying the message of the <see cref="ArgumentException"/> (including <see cref="ArgumentOutOfRangeException"/>)
    /// raised by <see cref="OtelDashboardOptions.Validate"/>.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, OtelDashboardOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
