using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.AspNetCore.Documentation;

public sealed record ApiFailureResponse(bool Success, object? Data, ApiErrorResponse Error);
