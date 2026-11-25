namespace Shipwise.Identity.Application.Abstractions.Security;

public sealed class SessionOptions
{
    public int IdleTimeoutDays { get; init; } = 7;
    public int AbsoluteTimeoutDays { get; init; } = 30;
    public int RefreshGraceSeconds { get; init; } = 30;
}
