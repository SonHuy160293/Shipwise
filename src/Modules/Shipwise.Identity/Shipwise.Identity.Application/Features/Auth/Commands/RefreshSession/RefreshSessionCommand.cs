using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.Identity.Application.Features.Auth.Commands.RefreshSession;

public sealed record RefreshSessionCommand(string? RefreshToken) : ICommand<RefreshResult>
{
    public override string ToString() => nameof(RefreshSessionCommand);
}
