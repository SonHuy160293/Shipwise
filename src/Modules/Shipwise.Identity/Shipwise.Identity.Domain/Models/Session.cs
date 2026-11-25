using System.Security.Cryptography;
using Shipwise.SharedKernel.Abstractions.Domain;

namespace Shipwise.Identity.Domain.Models;

public sealed class Session : Entity
{
    private Session() { }

    private Session(Guid userId, string hash, DateTimeOffset now, TimeSpan idle, TimeSpan absolute)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        RefreshTokenHash = hash;
        ExpiresAt = now + absolute;
        IdleExpiresAt = IdleDeadline(now, idle);
    }

    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = null!;
    public string? PreviousRefreshTokenHash { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public DateTimeOffset IdleExpiresAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public SessionRevocationReason? RevocationReason { get; private set; }
    public uint Version { get; private set; }

    public static Session Start(Guid userId, string refreshSecretHash, DateTimeOffset now, TimeSpan idleTimeout, TimeSpan absoluteTimeout)
        => new(userId, refreshSecretHash, now, idleTimeout, absoluteTimeout);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < IdleExpiresAt && now < ExpiresAt;

    public void Rotate(string newHash, DateTimeOffset now, TimeSpan idleTimeout)
    {
        PreviousRefreshTokenHash = RefreshTokenHash;
        RefreshTokenHash = newHash;
        RotatedAt = now;
        IdleExpiresAt = IdleDeadline(now, idleTimeout);
    }

    public bool AcceptsPrevious(string hash, DateTimeOffset now, TimeSpan grace)
        => RotatedAt is { } rotated && now >= rotated && now - rotated < grace
            && HashMatches(PreviousRefreshTokenHash, hash);

    public static bool HashMatches(string? stored, string candidate)
        => stored is not null && CryptographicOperations.FixedTimeEquals(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(stored.AsSpan()),
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(candidate.AsSpan()));

    public void Revoke(SessionRevocationReason reason, DateTimeOffset now)
    {
        if (RevokedAt is null)
        {
            RevokedAt = now;
            RevocationReason = reason;
        }
    }

    private DateTimeOffset IdleDeadline(DateTimeOffset now, TimeSpan idleTimeout)
        => now + idleTimeout < ExpiresAt ? now + idleTimeout : ExpiresAt;
}
