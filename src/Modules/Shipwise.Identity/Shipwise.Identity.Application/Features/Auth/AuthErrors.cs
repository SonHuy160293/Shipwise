using Shipwise.SharedKernel.Exceptions;

namespace Shipwise.Identity.Application.Features.Auth;

public static class AuthErrors
{
    public static UnauthorizedException SessionExpired() => new("Auth.SessionExpired", "The session has expired.");
    public static UnauthorizedException InvalidCredentials() => new("Auth.InvalidCredentials", "The credentials are invalid.");
}
