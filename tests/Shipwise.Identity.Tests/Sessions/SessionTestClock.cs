namespace Shipwise.Identity.Tests.Sessions;

internal sealed class SessionTestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan interval) => _now += interval;
}
