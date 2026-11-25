namespace Shipwise.Identity.Application.Features.Auth;

public sealed record SignInResult(AccountResponse Account, string AccessToken, DateTimeOffset AccessExpiresAt,
    string? RefreshToken, DateTimeOffset IdleExpiresAt)
{
    public override string ToString() => nameof(SignInResult);
}
