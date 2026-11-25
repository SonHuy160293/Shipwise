using Microsoft.Extensions.Logging;
using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.Identity.Application.Features.Auth.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler(ISessionRepository sessions, IUserRepository users,
    IRefreshTokenCodec codec, IAccessTokenIssuer issuer, SessionOptions options, TimeProvider clock, ILogger<RefreshSessionCommandHandler> logger)
    : ICommandHandler<RefreshSessionCommand, RefreshResult>
{
    public async Task<Result<RefreshResult>> HandleAsync(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        if (!codec.TryDecode(request.RefreshToken, out Guid id, out string hash))
        {
            throw AuthErrors.SessionExpired();
        }

        var session = await sessions.LoadForUpdateAsync(id, cancellationToken);
        var now = clock.GetUtcNow();
        if (session is null || !session.IsActive(now))
        {
            throw AuthErrors.SessionExpired();
        }

        string? refresh = null;
        if (codec.Matches(session.RefreshTokenHash, hash))
        {
            string secret = codec.CreateSecret(out string nextHash);
            session.Rotate(nextHash, now, TimeSpan.FromDays(options.IdleTimeoutDays));
            refresh = codec.Encode(id, secret);
        }
        else if (!session.AcceptsPrevious(hash, now, TimeSpan.FromSeconds(options.RefreshGraceSeconds)))
        {
            if (!codec.Matches(session.PreviousRefreshTokenHash, hash))
            {
                throw AuthErrors.SessionExpired();
            }

            session.Revoke(SessionRevocationReason.ReuseDetected, now);
            logger.LogWarning("Previous refresh token reused for session {SessionId}", id);
            return Result.Success(new RefreshResult(null));
        }

        var account = await users.GetAccountAsync(session.UserId, cancellationToken) ?? throw AuthErrors.SessionExpired();
        string access = issuer.Issue(account.UserId, id, now, out var expires);
        return Result.Success(new RefreshResult(new(account, access, expires, refresh, session.IdleExpiresAt)));
    }
}
