using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<bool> ExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}
