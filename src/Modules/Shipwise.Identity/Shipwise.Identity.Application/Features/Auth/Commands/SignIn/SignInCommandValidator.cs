using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Exceptions;

namespace Shipwise.Identity.Application.Features.Auth.Commands.SignIn;

public sealed class SignInCommandValidator : IValidator<SignInCommand>
{
    public Task ValidateAsync(SignInCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<ValidationError>();
        if (request.Email?.Trim() is not { Length: >= 1 and <= 254 })
        {
            errors.Add(new("email", "An email of 1 to 254 characters is required."));
        }

        if (request.Password is not { Length: >= 1 and <= 128 })
        {
            errors.Add(new("password", "A password of 1 to 128 characters is required."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
