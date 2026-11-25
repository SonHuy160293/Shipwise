using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.Identity.Application.Features.Auth.Commands.SignOut;

public sealed class SignOutCommandHandler(ISessionRepository sessions, IRefreshTokenCodec codec, TimeProvider clock)
    : ICommandHandler<SignOutCommand>
{
    public async Task<Result> HandleAsync(SignOutCommand request, CancellationToken cancellationToken)
    {
        if (codec.TryDecode(request.RefreshToken, out Guid id, out string hash))
        {
            var session = await sessions.LoadForUpdateAsync(id, cancellationToken);
            var now = clock.GetUtcNow();
            if (session?.IsActive(now) == true && (codec.Matches(session.RefreshTokenHash, hash) || codec.Matches(session.PreviousRefreshTokenHash, hash)))
            {
                session.Revoke(SessionRevocationReason.SignedOut, now);
            }
        }

        return Result.Success();
    }
}
