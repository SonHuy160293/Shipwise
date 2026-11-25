namespace Shipwise.Identity.API.Contracts.Auth;

// Documentation for the envelope returned by Ok() with no payload.
public sealed record SignOutResponse(bool Success, object? Data, object? Error);
