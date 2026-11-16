using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shipwise.SharedKernel.DependencyInjection;
using Shipwise.SharedKernel.Security;

namespace Shipwise.Identity.Tests.Host;

/// <summary>
/// Starts the real Identity API with test-only settings, plus the probe controller and
/// requests from this assembly so the pipeline can be exercised without a business endpoint.
/// </summary>
public class IdentityApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789-abcdef";
    public const string Issuer = "shipwise-tests";
    public const string Audience = "shipwise-tests";

    /// <summary>Gets the connection string the host uses. The default points at nothing.</summary>
    protected virtual string ConnectionString
        => "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1";

    public static string CreateToken(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(CustomClaims.SessionId, Guid.NewGuid().ToString()),
        };
        claims.AddRange(permissions.Select(permission => new Claim(CustomClaims.Permission, permission)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The host reads configuration while it registers services, so these must be host
        // settings: values added later through ConfigureAppConfiguration arrive too late.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ApplicationDb", ConnectionString);
        builder.UseSetting("Jwt:SecretKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "5");
        builder.UseSetting("IntegrationApi:ApiKeys:0", "test-only-integration-key");
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(IdentityApiFactory).Assembly);
            services.AddSelfMadeCqrs(typeof(IdentityApiFactory).Assembly);
            services.AddScoped<IAuthenticatedContextValidator, AlwaysActiveValidator>();
        });
    }

    private sealed class AlwaysActiveValidator : IAuthenticatedContextValidator
    {
        public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}
