using Microsoft.AspNetCore.Identity;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Security;
using Xunit;

namespace Shipwise.Identity.Tests.Sessions;

public sealed class DummyPasswordTests
{
    [Fact]
    public void MissingUserRunsPasswordVerificationAgainstAFixedHashAndAlwaysFails()
    {
        var hasher = new RecordingHasher();
        var service = new PasswordHashService(hasher);
        Assert.False(service.Verify(null, null, "attempt one", out _));
        Assert.False(service.Verify(null, null, "attempt two", out _));
        Assert.Equal(2, hasher.Hashes.Count);
        Assert.NotEmpty(hasher.Hashes[0]);
        Assert.Equal(hasher.Hashes[0], hasher.Hashes[1]);
        Assert.Equal(["attempt one", "attempt two"], hasher.Attempts);
    }

    private sealed class RecordingHasher : IPasswordHasher<User>
    {
        internal List<string> Hashes { get; } = [];
        internal List<string> Attempts { get; } = [];

        public string HashPassword(User user, string password) => throw new NotSupportedException();

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        {
            Hashes.Add(hashedPassword);
            Attempts.Add(providedPassword);
            return PasswordVerificationResult.Success;
        }
    }
}
