using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Application.Abstractions.Repositories;

public interface IPasswordCredentialRepository
{
    Task AddAsync(PasswordCredential credential, CancellationToken cancellationToken);
    Task<PasswordCredential?> FindAsync(Guid userId, CancellationToken cancellationToken);
}
