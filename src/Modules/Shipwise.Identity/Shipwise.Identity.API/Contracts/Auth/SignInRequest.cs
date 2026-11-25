namespace Shipwise.Identity.API.Contracts.Auth;

public sealed record SignInRequest(string Email, string Password)
{
    public override string ToString() => nameof(SignInRequest);
}
