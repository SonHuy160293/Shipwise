using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shipwise.AspNetCore.Security;
using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task MissingSessionValidatorRejectsOtherwiseValidatedPrincipal()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var context = new TokenValidatedContext(
            http,
            new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", Guid.NewGuid().ToString()), new Claim("sid", Guid.NewGuid().ToString())], "Bearer")),
        };
        var events = new CustomJwtBearerEvents(NullLogger<CustomJwtBearerEvents>.Instance);
        await events.TokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }
}
