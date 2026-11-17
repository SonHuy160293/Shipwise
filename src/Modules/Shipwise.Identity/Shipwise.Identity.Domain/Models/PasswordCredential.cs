using Shipwise.SharedKernel.Abstractions.Domain;
using Shipwise.SharedKernel.Validation;

namespace Shipwise.Identity.Domain.Models;

public sealed class PasswordCredential : Entity
{
    private PasswordCredential() { }

    private PasswordCredential(Guid id, Guid userId, string passwordHash)
        : base(id)
    {
        UserId = userId;
        PasswordHash = passwordHash;
    }

    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; } = null!;

    public static PasswordCredential Create(Guid userId, string passwordHash)
    {
        Guard.Id(userId, "userId");
        Guard.Require(
            !string.IsNullOrWhiteSpace(passwordHash) && passwordHash.Length <= 512,
            "passwordHash",
            "A bounded password hash is required.");
        return new PasswordCredential(Guid.CreateVersion7(), userId, passwordHash);
    }
}
