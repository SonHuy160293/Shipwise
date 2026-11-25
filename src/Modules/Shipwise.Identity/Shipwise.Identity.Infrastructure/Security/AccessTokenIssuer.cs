using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shipwise.AspNetCore.DependencyInjection;
using Shipwise.AspNetCore.Options;
using Shipwise.Identity.Application.Abstractions.Security;

namespace Shipwise.Identity.Infrastructure.Security;

public sealed class AccessTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    public string Issue(Guid userId, Guid sessionId, DateTimeOffset now, out DateTimeOffset expires)
    {
        var jwt = options.Value;
        expires = now.AddMinutes(jwt.AccessTokenExpirationMinutes);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("sid", sessionId.ToString()), new Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                AuthExtensions.AcceptedSigningAlgorithm),
        });
    }
}
