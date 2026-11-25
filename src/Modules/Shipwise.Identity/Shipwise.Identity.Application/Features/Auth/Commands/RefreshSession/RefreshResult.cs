namespace Shipwise.Identity.Application.Features.Auth.Commands.RefreshSession;

// A null issuance represents a committed reuse revocation. Other failures throw and roll back.
public sealed record RefreshResult(SignInResult? Issuance)
{
    public override string ToString() => nameof(RefreshResult);
}
