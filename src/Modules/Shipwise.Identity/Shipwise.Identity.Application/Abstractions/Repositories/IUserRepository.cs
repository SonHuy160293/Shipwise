using Shipwise.Identity.Application.Features.Auth;
using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<bool> ExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<AccountResponse?> GetAccountAsync(Guid userId, CancellationToken cancellationToken);
}
