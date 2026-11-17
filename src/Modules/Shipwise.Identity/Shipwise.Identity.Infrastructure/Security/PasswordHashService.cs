using Microsoft.AspNetCore.Identity;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Infrastructure.Security;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);
}
