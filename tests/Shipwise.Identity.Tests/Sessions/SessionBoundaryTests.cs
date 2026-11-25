using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shipwise.AspNetCore.Options;
using Shipwise.Identity.API.Configuration;
using Shipwise.Identity.Application.Features.Auth;
using Shipwise.Identity.Tests.Registration;
using Xunit;

namespace Shipwise.Identity.Tests.Sessions;

public sealed class SessionBoundaryTests
{
    [Fact]
    public async Task OriginRejectionPrecedesValidationAndAddressLimit()
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        const string signIn = "/api/v1/auth/sign-in";
        var fields = new { email = string.Empty, password = string.Empty };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(signIn, fields)).StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "https://foreign.test");
        var foreign = await client.PostAsJsonAsync(signIn, fields);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Contains("Request.OriginRejected", await foreign.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("Cookie", "shipwise-access=test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/register", fields)).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        for (int i = 0; i < 10; i++)
        {
            var invalid = await client.PostAsJsonAsync(signIn, fields);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var body = await invalid.Content.ReadFromJsonAsync<JsonObject>();
            var error = body!["error"] !;
            Assert.Equal("Validation.Failure", (string?)error["code"]);
            Assert.NotNull(error["errors"] !["email"]);
            Assert.NotNull(error["errors"] !["password"]);
        }

        var limited = await client.PostAsJsonAsync(signIn, fields);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Contains("Request.TooManyRequests", await limited.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("sign-in")]
    [InlineData("refresh")]
    [InlineData("sign-out")]
    public async Task OversizedRequestsAreRejectedBeforeCommands(string action)
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        using var payload = new ByteArrayContent(new byte[16385]);
        payload.Headers.ContentType = new("application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/v1/auth/" + action, payload)).StatusCode);
    }

    [Fact]
    public async Task MalformedSignInJsonReturnsOnlyPublicFields()
    {
        await using var factory = new RegistrationHostFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        using var payload = new StringContent("""{"private-field":{"password":  """, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/auth/sign-in", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Validation.Failure", body);
        Assert.DoesNotContain("private-field", body);
        var errors = JsonNode.Parse(body)?["error"]?["errors"]?.AsObject();
        Assert.NotNull(errors);
        Assert.Equal(["email", "password"], errors.Select(e => e.Key));
    }

    [Theory]
    [InlineData(true, "__Host-shipwise-access", "__Secure-shipwise-refresh")]
    [InlineData(false, "shipwise-access", "shipwise-refresh")]
    public void CookieWriterUsesMatchingAttributesWhenSettingAndClearing(bool secure, string access, string refresh)
    {
        var writer = new AuthCookieWriter(new(secure), Options.Create(new JwtOptions { AccessTokenExpirationMinutes = 10 }));
        var ctx = new DefaultHttpContext();
        var now = DateTimeOffset.UtcNow;
        writer.Write(ctx.Response, new SignInResult(new(Guid.NewGuid(), "user@example.test", "User"), "access", now.AddMinutes(10), "refresh", now.AddDays(7)));
        void CheckCookies(bool clearing)
        {
            var cookies = Microsoft.Net.Http.Headers.SetCookieHeaderValue.ParseList(ctx.Response.Headers.SetCookie.OfType<string>().ToList());
            Assert.Equal(2, cookies.Count);
            Assert.All(cookies, c =>
            {
                Assert.True(c.HttpOnly);
                Assert.Equal(secure, c.Secure);
                Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, c.SameSite);
            });
            var first = Assert.Single(cookies, c => c.Name == access);
            var second = Assert.Single(cookies, c => c.Name == refresh);
            Assert.Equal("/", first.Path.Value);
            Assert.Equal("/api/v1/auth", second.Path.Value);
            Assert.Equal(clearing ? TimeSpan.Zero : TimeSpan.FromMinutes(10), first.MaxAge);
            if (clearing)
            {
                Assert.Equal(TimeSpan.Zero, second.MaxAge);
            }
            else
            {
                Assert.Equal(now.AddDays(7).ToUnixTimeSeconds(), second.Expires!.Value.ToUnixTimeSeconds());
            }
        }

        CheckCookies(false);
        ctx.Response.Headers.Clear();
        writer.Clear(ctx.Response);
        CheckCookies(true);
    }
}
