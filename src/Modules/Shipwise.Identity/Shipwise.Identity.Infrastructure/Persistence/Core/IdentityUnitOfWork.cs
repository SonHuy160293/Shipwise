using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Abstractions.Persistence;
using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.Identity.Infrastructure.Persistence.Core;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            TResponse response = await action(cancellationToken);
            if (response is Result { IsFailure: true })
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return response;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            // Disposing the uncommitted transaction rolls it back; an explicit rollback here
            // could throw and hide the original failure.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
