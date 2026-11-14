namespace Shipwise.AspNetCore.HttpClient.Options.Resilience;

public sealed class TimeoutOptions
{
    public TimeSpan Timeout { get; init; }
}
