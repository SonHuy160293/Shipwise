using Microsoft.EntityFrameworkCore;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Security;

namespace Shipwise.Identity.Infrastructure.Security;

public sealed class SessionContextValidator(IdentityDbContext context, TimeProvider clock) : IAuthenticatedContextValidator
{
    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await context.Set<Session>().AsNoTracking().SingleOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == userId, cancellationToken);
        return session?.IsActive(clock.GetUtcNow()) == true;
    }
}
