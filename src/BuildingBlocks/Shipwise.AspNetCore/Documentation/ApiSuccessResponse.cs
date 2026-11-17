using System.ComponentModel.DataAnnotations;

namespace Shipwise.AspNetCore.Documentation;

public sealed record ApiSuccessResponse<T>(bool Success, [property: Required] T Data, object? Error);
