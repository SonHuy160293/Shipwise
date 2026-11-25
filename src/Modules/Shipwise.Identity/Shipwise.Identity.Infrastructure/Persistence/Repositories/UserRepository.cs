using Microsoft.EntityFrameworkCore;
using Shipwise.EntityFrameworkCore.Repositories;
using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Features.Auth;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;

namespace Shipwise.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext context) : EfRepositoryBase<User, Guid, IdentityDbContext>(context), IUserRepository
{
    public Task<bool> ExistsAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Query(user => user.NormalizedEmail == normalizedEmail).AnyAsync(cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken) => AddEntityAsync(user, cancellationToken);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Query(user => user.NormalizedEmail == normalizedEmail).SingleOrDefaultAsync(cancellationToken);

    public Task<AccountResponse?> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
        => Query(user => user.Id == userId).Select(user => new AccountResponse(user.Id, user.Email, user.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
}
