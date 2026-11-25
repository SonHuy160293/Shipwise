using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.Identity.Application.Features.Auth.Commands.SignIn;

public sealed class SignInCommandHandler(IUserRepository users, IPasswordCredentialRepository credentials,
    IPasswordHashService passwords, ISignInThrottle throttle, ISessionRepository sessions, IRefreshTokenCodec codec,
    IAccessTokenIssuer issuer, TimeProvider clock, SessionOptions options) : ICommandHandler<SignInCommand, SignInResult>
{
    public async Task<Result<SignInResult>> HandleAsync(SignInCommand request, CancellationToken cancellationToken)
    {
        string email = request.Email!.Trim().ToLowerInvariant();
        throttle.Check(email);
        var user = await users.FindByEmailAsync(email, cancellationToken);
        var credential = user is null ? null : await credentials.FindAsync(user.Id, cancellationToken);
        if (!passwords.Verify(user, credential?.PasswordHash, request.Password!, out bool upgrade) || user is null || credential is null)
        {
            throttle.RecordFailure(email);
            throw AuthErrors.InvalidCredentials();
        }

        throttle.Clear(email);
        if (upgrade)
        {
            credential.UpgradeHash(passwords.Hash(user, request.Password!));
        }

        var now = clock.GetUtcNow();
        if (codec.TryDecode(request.RefreshToken, out Guid previousId, out string previousHash))
        {
            var previous = await sessions.LoadForUpdateAsync(previousId, cancellationToken);
            if (previous?.IsActive(now) == true && (codec.Matches(previous.RefreshTokenHash, previousHash)
                || previous.AcceptsPrevious(previousHash, now, TimeSpan.FromSeconds(options.RefreshGraceSeconds))))
            {
                previous.Revoke(SessionRevocationReason.Replaced, now);
            }
        }

        await sessions.DeleteInactiveAsync(user.Id, now, cancellationToken);
        string secret = codec.CreateSecret(out string hash);
        var session = Session.Start(user.Id, hash, now, TimeSpan.FromDays(options.IdleTimeoutDays), TimeSpan.FromDays(options.AbsoluteTimeoutDays));
        await sessions.AddAsync(session, cancellationToken);
        string access = issuer.Issue(user.Id, session.Id, now, out var expires);
        var account = new AccountResponse(user.Id, user.Email, user.DisplayName);
        return Result.Success(new SignInResult(account, access, expires, codec.Encode(session.Id, secret), session.IdleExpiresAt));
    }
}
