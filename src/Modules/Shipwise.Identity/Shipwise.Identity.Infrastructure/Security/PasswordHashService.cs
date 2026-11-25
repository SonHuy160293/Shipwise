using Microsoft.AspNetCore.Identity;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Infrastructure.Security;

public sealed class PasswordHashService(IPasswordHasher<User> hasher) : IPasswordHashService
{
    private static readonly User DummyUser = User.Create("unused@example.invalid", "Unused");
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(DummyUser, Guid.NewGuid().ToString());

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User? user, string? storedHash, string password, out bool rehashNeeded)
    {
        var result = hasher.VerifyHashedPassword(user ?? DummyUser, storedHash ?? DummyHash, password);
        rehashNeeded = result == PasswordVerificationResult.SuccessRehashNeeded;
        return user is not null && storedHash is not null && result != PasswordVerificationResult.Failed;
    }
}
