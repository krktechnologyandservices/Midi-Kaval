using Microsoft.Extensions.Options;

namespace MidiKaval.Api.Infrastructure;

public sealed class DataRateLimitOptions
{
    public const string SectionName = "DataRateLimiting";

    public int ReadPermitLimit { get; set; } = 100;
    public int WritePermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 60;
}

public sealed class DataRateLimitOptionsValidator : IValidateOptions<DataRateLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, DataRateLimitOptions options)
    {
        if (options.ReadPermitLimit <= 0)
        {
            return ValidateOptionsResult.Fail("DataRateLimiting:ReadPermitLimit must be positive.");
        }

        if (options.WritePermitLimit <= 0)
        {
            return ValidateOptionsResult.Fail("DataRateLimiting:WritePermitLimit must be positive.");
        }

        if (options.WindowSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("DataRateLimiting:WindowSeconds must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
