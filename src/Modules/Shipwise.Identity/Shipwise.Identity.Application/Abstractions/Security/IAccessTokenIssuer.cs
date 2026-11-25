namespace Shipwise.Identity.Application.Abstractions.Security;

public interface IAccessTokenIssuer
{
    string Issue(Guid userId, Guid sessionId, DateTimeOffset now, out DateTimeOffset expires);
}
