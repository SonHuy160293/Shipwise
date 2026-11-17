namespace Shipwise.Identity.API.Contracts.Auth;

public sealed record RegisterUserRequest(string Email, string DisplayName, string Password);
