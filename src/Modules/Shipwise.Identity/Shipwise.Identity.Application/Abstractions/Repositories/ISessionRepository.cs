using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Application.Abstractions.Repositories;

public interface ISessionRepository
{
    Task<Session?> LoadForUpdateAsync(Guid sessionId, CancellationToken cancellationToken);
    Task AddAsync(Session session, CancellationToken cancellationToken);
    Task DeleteInactiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
