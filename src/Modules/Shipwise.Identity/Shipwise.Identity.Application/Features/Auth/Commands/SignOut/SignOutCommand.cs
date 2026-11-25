using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.Identity.Application.Features.Auth.Commands.SignOut;

public sealed record SignOutCommand(string? RefreshToken) : ICommand
{
    public override string ToString() => nameof(SignOutCommand);
}
