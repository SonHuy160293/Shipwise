using Shipwise.Identity.Application.Abstractions.Repositories;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Exceptions;

namespace Shipwise.Identity.Application.Features.Auth.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IPasswordCredentialRepository credentials,
    IPasswordHashService passwordHashService) : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    public async Task<Result<RegisterUserResponse>> HandleAsync(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        User user = User.Create(request.Email, request.DisplayName);
        if (await users.ExistsAsync(user.NormalizedEmail, cancellationToken))
        {
            throw new ConflictException("Registration.EmailTaken", "An account with this email already exists.");
        }

        var credential = PasswordCredential.Create(user.Id, passwordHashService.Hash(user, request.Password!));
        await users.AddAsync(user, cancellationToken);
        await credentials.AddAsync(credential, cancellationToken);
        return Result.Success(new RegisterUserResponse(user.Id));
    }
}
