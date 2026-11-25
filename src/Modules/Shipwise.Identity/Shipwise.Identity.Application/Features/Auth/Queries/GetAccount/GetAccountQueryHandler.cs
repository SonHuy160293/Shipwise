using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Exceptions;
using Shipwise.SharedKernel.Security;

namespace Shipwise.Identity.Application.Features.Auth.Queries.GetAccount;

public sealed class GetAccountQueryHandler(IUserRepository users, ICurrentUserProvider currentUser) : IQueryHandler<GetAccountQuery, AccountResponse>
{
    public async Task<Result<AccountResponse>> HandleAsync(GetAccountQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.GetCurrentUser()?.Id, out Guid caller) || caller != request.UserId)
        {
            throw new UnauthorizedException("Request.Unauthorized", "Authentication is required.");
        }

        return Result.Success(await users.GetAccountAsync(caller, cancellationToken)
            ?? throw new UnauthorizedException("Request.Unauthorized", "Authentication is required."));
    }
}
