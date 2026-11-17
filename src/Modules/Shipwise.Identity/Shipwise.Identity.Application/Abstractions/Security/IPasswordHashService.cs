using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Application.Abstractions.Security;

public interface IPasswordHashService
{
    string Hash(User user, string password);
}
