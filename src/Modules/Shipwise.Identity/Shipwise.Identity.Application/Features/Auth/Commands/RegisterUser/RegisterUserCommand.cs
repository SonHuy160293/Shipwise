using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.Identity.Application.Features.Auth.Commands.RegisterUser;

public sealed record RegisterUserCommand(string? Email, string? DisplayName, string? Password) : ICommand<RegisterUserResponse>
{
    public override string ToString() => nameof(RegisterUserCommand);
}
