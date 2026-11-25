using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shipwise.AspNetCore.Controllers;
using Shipwise.AspNetCore.Documentation;
using Shipwise.AspNetCore.Security;
using Shipwise.Identity.API.Configuration;
using Shipwise.Identity.API.Contracts.Auth;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Application.Features.Auth;
using Shipwise.Identity.Application.Features.Auth.Commands.RefreshSession;
using Shipwise.Identity.Application.Features.Auth.Commands.RegisterUser;
using Shipwise.Identity.Application.Features.Auth.Commands.SignIn;
using Shipwise.Identity.Application.Features.Auth.Commands.SignOut;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Cqrs;
using Shipwise.SharedKernel.Exceptions;

namespace Shipwise.Identity.API.Controllers;

public sealed class AuthController(ISender sender, AuthCookieWriter cookies) : PublicApiController
{
    [HttpPost("sign-in")]
    [RequireAllowedOrigin]
    [EnableRateLimiting("sign-in")]
    [SessionBodyLimit]
    [ProducesResponseType(typeof(ApiSuccessResponse<AccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SignIn(SignInRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.SendAsync(new SignInCommand(request.Email, request.Password, cookies.ReadRefresh(Request)), cancellationToken);
            if (result.IsFailure)
            {
                return BadRequest(result.Error);
            }

            cookies.Write(Response, result.Value);
            return Ok(result.Value.Account);
        }
        catch (SignInThrottledException exception)
        {
            Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(exception.RetryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorResponse("Request.TooManyRequests", "Too many requests."));
        }
    }

    [HttpPost("refresh")]
    [RequireAllowedOrigin]
    [SessionBodyLimit]
    [ProducesResponseType(typeof(ApiSuccessResponse<AccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var command = new RefreshSessionCommand(cookies.ReadRefresh(Request));
        try
        {
            var result = await sender.SendAsync(command, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(result.Error);
            }

            if (result.Value.Issuance is not { } issuance)
            {
                cookies.Clear(Response);
                var expired = AuthErrors.SessionExpired();
                return Unauthorized(new ApiErrorResponse(expired.Code, expired.Message));
            }

            cookies.Write(Response, issuance);
            return Ok(issuance.Account);
        }
        catch (UnauthorizedException exception) when (exception.Code == "Auth.SessionExpired")
        {
            cookies.Clear(Response);
            throw;
        }
    }

    [HttpPost("sign-out")]
    [RequireAllowedOrigin]
    [SessionBodyLimit]
    [ProducesResponseType(typeof(SignOutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiFailureResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SignOutSession(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SignOutCommand(cookies.ReadRefresh(Request)), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        cookies.Clear(Response);
        return Ok();
    }

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
