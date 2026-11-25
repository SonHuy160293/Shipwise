using System.Buffers.Text;
using System.Security.Cryptography;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;

namespace Shipwise.Identity.Infrastructure.Security;

public sealed class RefreshTokenCodec : IRefreshTokenCodec
{
    public string CreateSecret(out string hash)
    {
        byte[] secret = RandomNumberGenerator.GetBytes(32);
        hash = Convert.ToHexStringLower(SHA256.HashData(secret));
        return Base64Url.EncodeToString(secret);
    }

    public string Encode(Guid sessionId, string secret) => $"{sessionId:N}.{secret}";

    public bool TryDecode(string? token, out Guid sessionId, out string hash)
    {
        sessionId = Guid.Empty;
        hash = string.Empty;
        if (token is not { Length: 76 } || token[32] != '.' || !Guid.TryParseExact(token.AsSpan(0, 32), "N", out sessionId))
        {
            return false;
        }

        Span<byte> secret = stackalloc byte[32];
        if (!Base64Url.TryDecodeFromChars(token.AsSpan(33), secret, out int length) || length != secret.Length
            || !token.AsSpan(33).SequenceEqual(Base64Url.EncodeToString(secret)))
        {
            return false;
        }

        hash = Convert.ToHexStringLower(SHA256.HashData(secret));
        return true;
    }

    public bool Matches(string? stored, string candidate) => Session.HashMatches(stored, candidate);
}
