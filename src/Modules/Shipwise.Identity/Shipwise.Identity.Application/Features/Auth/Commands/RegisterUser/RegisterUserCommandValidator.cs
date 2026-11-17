using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Exceptions;

namespace Shipwise.Identity.Application.Features.Auth.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : IValidator<RegisterUserCommand>
{
    public Task ValidateAsync(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<ValidationError>();
        string email = request.Email?.Trim() ?? string.Empty;
        int at = email.IndexOf('@');
        if (email.Length is < 3 or > 254 || at <= 0 || at != email.LastIndexOf('@')
            || !email.AsSpan(at + 1).Contains('.') || email.Any(char.IsWhiteSpace))
        {
            errors.Add(new("email", "A valid email is required."));
        }

        string name = request.DisplayName?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl))
        {
            errors.Add(new("displayName", "A display name of 1 to 100 characters is required."));
        }

        if (request.Password is not { Length: >= 12 and <= 128 })
        {
            errors.Add(new("password", "A password of 12 to 128 characters is required."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
