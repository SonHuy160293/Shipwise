using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Application.Abstractions.Security;

public interface IPasswordHashService
{
    string Hash(User user, string password);
    bool Verify(User? user, string? storedHash, string password, out bool rehashNeeded);
}
