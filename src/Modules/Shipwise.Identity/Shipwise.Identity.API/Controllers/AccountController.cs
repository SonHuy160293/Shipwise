using Microsoft.AspNetCore.Mvc;
using Shipwise.AspNetCore.Controllers;
using Shipwise.AspNetCore.Documentation;
using Shipwise.AspNetCore.Security;
using Shipwise.Identity.Application.Features.Auth;
using Shipwise.Identity.Application.Features.Auth.Queries.GetAccount;
using Shipwise.SharedKernel.Cqrs;
using Shipwise.SharedKernel.Security;

namespace Shipwise.Identity.API.Controllers;

public sealed class AccountController(ISender sender, ICurrentUserProvider currentUser) : ApiController(sender)
{
    [HttpGet]
    [AuthenticatedUserOnly]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(ApiSuccessResponse<AccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.GetCurrentUser()?.Id, out Guid caller))
        {
            return Challenge();
        }

        var result = await Sender.SendAsync(new GetAccountQuery(caller), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }
}
