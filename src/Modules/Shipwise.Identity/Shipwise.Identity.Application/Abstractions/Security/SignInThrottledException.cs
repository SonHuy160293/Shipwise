namespace Shipwise.Identity.Application.Abstractions.Security;

public sealed class SignInThrottledException(TimeSpan retryAfter) : Exception("Too many requests.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
