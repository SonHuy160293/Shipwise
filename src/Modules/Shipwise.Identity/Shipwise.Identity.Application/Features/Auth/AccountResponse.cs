namespace Shipwise.Identity.Application.Features.Auth;

public sealed record AccountResponse(Guid UserId, string Email, string DisplayName)
{
    public override string ToString() => nameof(AccountResponse);
}
