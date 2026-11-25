namespace Shipwise.Identity.Application.Abstractions.Security;

public interface ISignInThrottle
{
    void Check(string normalizedEmail);
    void RecordFailure(string normalizedEmail);
    void Clear(string normalizedEmail);
}
