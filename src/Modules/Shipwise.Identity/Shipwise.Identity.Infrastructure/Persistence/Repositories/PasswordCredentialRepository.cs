using Shipwise.EntityFrameworkCore.Repositories;
using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;

namespace Shipwise.Identity.Infrastructure.Persistence.Repositories;

public sealed class PasswordCredentialRepository(IdentityDbContext context)
    : EfRepositoryBase<PasswordCredential, Guid, IdentityDbContext>(context), IPasswordCredentialRepository
{
    public Task AddAsync(PasswordCredential credential, CancellationToken cancellationToken)
        => AddEntityAsync(credential, cancellationToken);
}
