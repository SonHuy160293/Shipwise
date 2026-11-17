using Shipwise.SharedKernel.Abstractions.Domain;
using Shipwise.SharedKernel.Validation;

namespace Shipwise.Identity.Domain.Models;

public sealed class User : Entity
{
    private User() { }

    private User(Guid id, string email, string displayName)
        : base(id)
    {
        Email = email;
        NormalizedEmail = email.ToLowerInvariant();
        DisplayName = displayName;
    }

    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public uint Version { get; private set; }

    public static User Create(string? email, string? displayName)
    {
        string value = Guard.Text(email, 254, "email");
        int at = value.IndexOf('@');
        Guard.Require(
            value.Length >= 3 && at > 0 && at == value.LastIndexOf('@')
                && value.AsSpan(at + 1).Contains('.') && !value.Any(char.IsWhiteSpace),
            "email",
            "A valid email is required.");
        string name = Guard.Text(displayName, 100, "displayName", rejectControlCharacters: true);
        return new User(Guid.CreateVersion7(), value, name);
    }
}
