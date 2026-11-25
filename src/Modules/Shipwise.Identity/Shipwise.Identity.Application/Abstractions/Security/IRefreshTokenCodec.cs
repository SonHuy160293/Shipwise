namespace Shipwise.Identity.Application.Abstractions.Security;

public interface IRefreshTokenCodec
{
    string CreateSecret(out string hash);
    string Encode(Guid sessionId, string secret);
    bool TryDecode(string? token, out Guid sessionId, out string hash);
    bool Matches(string? stored, string candidate);
}
