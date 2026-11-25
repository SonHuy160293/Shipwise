using Microsoft.EntityFrameworkCore;
using Shipwise.EntityFrameworkCore.Repositories;
using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;

namespace Shipwise.Identity.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository(IdentityDbContext context) : EfRepositoryBase<Session, Guid, IdentityDbContext>(context), ISessionRepository
{
    // PostgreSQL excludes xmin from s.*. EF needs it for the concurrency check at commit.
    public Task<Session?> LoadForUpdateAsync(Guid sessionId, CancellationToken cancellationToken)
        => DbSet.FromSql($"""SELECT s.*, s.xmin FROM identity."Sessions" AS s WHERE s."Id" = {sessionId} FOR UPDATE""")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);

    public Task AddAsync(Session session, CancellationToken cancellationToken) => AddEntityAsync(session, cancellationToken);

    public async Task DeleteInactiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Sessions are hard-deleted. Restrict the cleanup to pre-existing inactive rows in this user's transaction.
        await DbSet.Where(s => s.UserId == userId && (s.RevokedAt != null || s.ExpiresAt <= now || s.IdleExpiresAt <= now))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
