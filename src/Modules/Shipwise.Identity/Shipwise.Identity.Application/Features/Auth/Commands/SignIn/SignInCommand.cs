using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.Identity.Application.Features.Auth.Commands.SignIn;

public sealed record SignInCommand(string? Email, string? Password, string? RefreshToken) : ICommand<SignInResult>
{
    public override string ToString() => nameof(SignInCommand);
}
