using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shipwise.AspNetCore.Controllers;
using Shipwise.AspNetCore.Documentation;
using Shipwise.Identity.API.Contracts.Auth;
using Shipwise.Identity.Application.Features.Auth.Commands.RegisterUser;
using Shipwise.SharedKernel.Cqrs;

namespace Shipwise.Identity.API.Controllers;

public sealed class AuthController(ISender sender) : PublicApiController
{
    [HttpPost("register")]
    [EnableRateLimiting("registration")]
    [RequestSizeLimit(16384)]
    [ProducesResponseType(typeof(ApiSuccessResponse<RegisterUserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new RegisterUserCommand(request.Email, request.DisplayName, request.Password), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
