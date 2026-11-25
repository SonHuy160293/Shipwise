using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;
using Serilog.Core;
using Serilog.Events;
using Shipwise.Identity.Application.Abstractions.Security;
using Shipwise.Identity.Domain.Models;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.Identity.Tests.Persistence;
using Shipwise.Identity.Tests.Registration;
using Xunit;

namespace Shipwise.Identity.Tests.Sessions;

public sealed class SessionHttpTests(PostgreSqlDatabaseFixture database) : IClassFixture<PostgreSqlDatabaseFixture>, IAsyncLifetime, IAsyncDisposable
{
    private const string Password = "session-private-password-42";
    private const string DisplayName = "Private Session Tester";
    private readonly string _email = $"session-{Guid.NewGuid():N}@example.test";
    private readonly SessionTestClock _clock = new();
    private readonly LogCollector _logs = new();
    private readonly RegistrationHostFactory _root = new(database.ConnectionString);
    private WebApplicationFactory<Program> _host = null!;
    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(database.ConnectionString))
        {
            return;
        }

        _host = _root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(_clock);
                services.AddSingleton<ILogEventSink>(_logs);
            });
        });
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        _client = _host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        _client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        var registered = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = _email, displayName = DisplayName, password = Password });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        _userId = Guid.Parse((string)(await JsonAsync(registered))["data"] !["userId"] !);
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _root.DisposeAsync();
    }

    [PostgreSqlFact]
    public async Task SignInIssuesCredentialsForOneSessionAndAccountAcceptsCookieOrBearer()
    {
        var response = await SignInAsync(email: " " + _email.ToUpperInvariant() + " ");
        await AssertAccountAsync(response);
        var access = Cookie(response, "shipwise-access");
        var refresh = Cookie(response, "shipwise-refresh");
        Assert.Equal(2, Cookies(response).Count);
        Assert.True(access.HttpOnly && refresh.HttpOnly);
        Assert.False(access.Secure || refresh.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, refresh.SameSite);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, access.SameSite);
        Assert.Equal("/", access.Path.Value);
        Assert.Equal("/api/v1/auth", refresh.Path.Value);
        Assert.Equal(TimeSpan.FromMinutes(10), access.MaxAge);
        Assert.Equal(_clock.GetUtcNow().AddDays(7), refresh.Expires);
        var jwt = new JsonWebToken(access.Value.Value!);
        Assert.Equal(_userId.ToString(), jwt.Subject);
        Assert.Equal("shipwise-tests", jwt.Issuer);
        Assert.Contains("shipwise-tests", jwt.Audiences);
        Assert.Equal(TimeSpan.FromMinutes(10), jwt.ValidTo - jwt.IssuedAt);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, jwt.IssuedAt);
        var session = await ReadSessionAsync();
        Assert.Equal(session.Id.ToString(), jwt.GetClaim("sid").Value);
        string secret = refresh.Value.Value!.Split('.')[1];
        byte[] bytes = System.Buffers.Text.Base64Url.DecodeFromChars(secret);
        Assert.Equal(32, bytes.Length);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), session.RefreshTokenHash);
        await AssertAccountAsync(await AccountAsync(access.Value.Value!));
        await AssertAccountAsync(await AccountAsync(access.Value.Value!, bearer: true));
        using var precedence = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        precedence.Headers.Add("Cookie", "shipwise-access=" + access.Value);
        precedence.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(precedence)).StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, body);
        Assert.DoesNotContain(access.Value.Value!, body);
        Assert.DoesNotContain(secret, body);
        Assert.NotEmpty(_logs.Events);
        string logs = string.Join(Environment.NewLine, _logs.Events);
        foreach (string value in new[] { _email, DisplayName, Password, access.Value.Value!, secret, session.RefreshTokenHash })
        {
            Assert.False(logs.Contains(value, StringComparison.OrdinalIgnoreCase), "Sensitive value in captured logs.");
        }
    }

    [PostgreSqlFact]
    public async Task UnknownAndWrongCredentialsGiveSameFailureWithoutSessionsOrCookies()
    {
        var wrong = await SignInAsync(password: "wrong password");
        var unknown = await SignInAsync(email: "missing@example.test");
        foreach (var error in new[] { wrong, unknown })
        {
            await AssertErrorAsync(error, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
            Assert.False(error.Headers.Contains("Set-Cookie"));
        }

        Assert.Equal((string?)(await JsonAsync(wrong))["error"] !["message"], (string?)(await JsonAsync(unknown))["error"] !["message"]);
        using var scope = _host.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<Session>().AnyAsync(s => s.UserId == _userId));
    }

    [PostgreSqlFact]
    public async Task SuccessfulSignInUpgradesLegacyPasswordHash()
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Set<User>().SingleAsync(u => u.Id == _userId);
        var credential = await db.Set<PasswordCredential>().SingleAsync(c => c.UserId == _userId);
        var legacy = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        string old = legacy.HashPassword(user, Password);
        credential.UpgradeHash(old);
        await db.SaveChangesAsync();
        await AssertAccountAsync(await SignInAsync());
        await db.Entry(credential).ReloadAsync();
        Assert.NotEqual(old, credential.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(user, credential.PasswordHash, Password));
    }

    [PostgreSqlFact]
    public async Task ReplacementSignInAndSignOutRevokeOnlyPresentedSessions()
    {
        var initial = await SignInAsync();
        string old = Cookie(initial, "shipwise-refresh").Value.Value!;
        var replacement = await SignInAsync(refresh: old);
        await AssertAccountAsync(replacement);
        Assert.Equal(SessionRevocationReason.Replaced, (await ReadSessionAsync(old)).RevocationReason);
        string refresh = Cookie(replacement, "shipwise-refresh").Value.Value!;
        string access = Cookie(replacement, "shipwise-access").Value.Value!;
        foreach (string? token in new[] { refresh, refresh, null, Guid.NewGuid().ToString("N") + refresh[32..] })
        {
            var signOut = await PostCookieAsync("sign-out", token);
            Assert.Equal(HttpStatusCode.OK, signOut.StatusCode);
            Assert.Null((await JsonAsync(signOut))["data"]);
            AssertCleared(signOut);
        }

        Assert.Equal(SessionRevocationReason.SignedOut, (await ReadSessionAsync(refresh)).RevocationReason);
        await AssertErrorAsync(await AccountAsync(access), HttpStatusCode.Unauthorized, "Request.Unauthorized");
        await AssertExpiredAsync(await PostCookieAsync("refresh", refresh));
        await AssertAccountAsync(await SignInAsync());
        Assert.Null(await FindSessionAsync(Guid.ParseExact(refresh[..32], "N")));
    }

    [PostgreSqlFact]
    public async Task RotationGraceAndReusePersistAcrossRequests()
    {
        var initial = await SignInAsync();
        string old = Cookie(initial, "shipwise-refresh").Value.Value!;
        var before = await ReadSessionAsync();
        var rotated = await PostCookieAsync("refresh", old);
        await AssertAccountAsync(rotated);
        string current = Cookie(rotated, "shipwise-refresh").Value.Value!;
        Assert.NotEqual(old, current);
        var after = await ReadSessionAsync();
        Assert.Equal(before.RefreshTokenHash, after.PreviousRefreshTokenHash);
        Assert.NotEqual(before.RefreshTokenHash, after.RefreshTokenHash);
        _clock.Advance(TimeSpan.FromSeconds(29));
        var grace = await PostCookieAsync("refresh", old);
        await AssertAccountAsync(grace);
        Assert.Single(Cookies(grace));
        string access = Cookie(grace, "shipwise-access").Value.Value!;
        _clock.Advance(TimeSpan.FromSeconds(1));
        await AssertExpiredAsync(await PostCookieAsync("refresh", old));
        Assert.Equal(SessionRevocationReason.ReuseDetected, (await ReadSessionAsync()).RevocationReason);
        await AssertErrorAsync(await AccountAsync(access), HttpStatusCode.Unauthorized, "Request.Unauthorized");
        await AssertExpiredAsync(await PostCookieAsync("refresh", current));
    }

    [PostgreSqlFact]
    public async Task ParallelRefreshesRotateOnceAndKeepSessionActive()
    {
        string token = Cookie(await SignInAsync(), "shipwise-refresh").Value.Value!;
        var pair = await Task.WhenAll(PostCookieAsync("refresh", token), PostCookieAsync("refresh", token));
        Assert.All(pair, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode));
        Assert.Single(pair, r => Cookies(r).Any(c => c.Name == "shipwise-refresh"));
        Assert.True((await ReadSessionAsync()).IsActive(_clock.GetUtcNow()));
    }

    [PostgreSqlFact]
    public async Task RefreshChecksMissingMalformedUnknownAndWrongSecretWithoutRevoking()
    {
        string token = Cookie(await SignInAsync(), "shipwise-refresh").Value.Value!;
        string wrong = token[..33] + (token[33] == 'a' ? "b" : "a") + token[34..];
        foreach (string? invalid in new[] { null, "bad", Guid.NewGuid().ToString("N") + token[32..], wrong })
        {
            await AssertExpiredAsync(await PostCookieAsync("refresh", invalid));
        }

        Assert.True((await ReadSessionAsync()).IsActive(_clock.GetUtcNow()));
    }

    [PostgreSqlFact]
    public async Task IdleAndAbsoluteDeadlinesRejectRefreshAndCryptographicallyLiveAccess()
    {
        string token = Cookie(await SignInAsync(), "shipwise-refresh").Value.Value!;
        var session = await ReadSessionAsync();
        _clock.Advance(TimeSpan.FromDays(7));
        await AssertDeadlineAsync(session, token);
        var signedIn = await SignInAsync();
        token = Cookie(signedIn, "shipwise-refresh").Value.Value!;
        session = await ReadSessionAsync();
        for (int i = 0; i < 4; i++)
        {
            _clock.Advance(TimeSpan.FromDays(6));
            var rotated = await PostCookieAsync("refresh", token);
            await AssertAccountAsync(rotated);
            token = Cookie(rotated, "shipwise-refresh").Value.Value!;
        }

        Assert.Equal(session.ExpiresAt, (await ReadSessionAsync()).IdleExpiresAt);
        _clock.Advance(TimeSpan.FromDays(6));
        await AssertDeadlineAsync(session, token);
    }

    [PostgreSqlFact]
    public async Task TenEmailFailuresAcrossAddressesThrottleOnlyThatEmail()
    {
        for (int i = 0; i < 10; i++)
        {
            _client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            _client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{i + 1}");
            await AssertErrorAsync(await SignInAsync(password: "wrong"), HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        }

        var throttled = await SignInAsync(email: _email.ToUpperInvariant());
        await AssertErrorAsync(throttled, HttpStatusCode.TooManyRequests, "Request.TooManyRequests");
        Assert.Equal(TimeSpan.FromMinutes(15), throttled.Headers.RetryAfter?.Delta);
        string other = "other-" + _email;
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = other, password = Password, displayName = "Other" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email: other)).StatusCode);
    }

    private async Task AssertDeadlineAsync(Session session, string token)
    {
        var issuer = _host.Services.GetRequiredService<IAccessTokenIssuer>();
        string live = issuer.Issue(_userId, session.Id, _clock.GetUtcNow(), out _);
        await AssertErrorAsync(await AccountAsync(live), HttpStatusCode.Unauthorized, "Request.Unauthorized");
        await AssertExpiredAsync(await PostCookieAsync("refresh", token));
    }

    private async Task<Session> ReadSessionAsync(string? refresh = null)
    {
        using var scope = _host.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<Session>().AsNoTracking().Where(s => s.UserId == _userId);
        if (refresh is not null)
        {
            Guid id = Guid.ParseExact(refresh[..32], "N");
            query = query.Where(s => s.Id == id);
        }

        return await query.SingleAsync();
    }

    private async Task<Session?> FindSessionAsync(Guid id)
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<Session>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
    }

    private async Task<HttpResponseMessage> SignInAsync(string? email = null, string? password = null, string? refresh = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in");
        request.Content = JsonContent.Create(new { email = email ?? _email, password = password ?? Password });
        if (refresh is not null)
        {
            request.Headers.Add("Cookie", "shipwise-refresh=" + refresh);
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AccountAsync(string access, bool bearer = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        if (bearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        }
        else
        {
            request.Headers.Add("Cookie", "shipwise-access=" + access);
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostCookieAsync(string action, string? refresh)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/" + action);
        if (refresh is not null)
        {
            request.Headers.Add("Cookie", "shipwise-refresh=" + refresh);
        }

        return await _client.SendAsync(request);
    }

    private async Task AssertAccountAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (JsonObject)(await JsonAsync(response))["data"] !;
        Assert.Equal(3, data.Count);
        Assert.Equal(_userId.ToString(), (string?)data["userId"]);
        Assert.Equal(_email, (string?)data["email"]);
        Assert.Equal(DisplayName, (string?)data["displayName"]);
    }

    private static async Task AssertExpiredAsync(HttpResponseMessage response)
    {
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "Auth.SessionExpired");
        AssertCleared(response);
    }

    private static void AssertCleared(HttpResponseMessage response)
    {
        var cookies = Cookies(response);
        Assert.Equal(2, cookies.Count);
        Assert.All(cookies, c => Assert.Equal(TimeSpan.Zero, c.MaxAge));
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (string?)(await JsonAsync(response))["error"] !["code"]);
    }

    private static SetCookieHeaderValue Cookie(HttpResponseMessage response, string name)
        => Assert.Single(Cookies(response), c => c.Name == name);

    private static IList<SetCookieHeaderValue> Cookies(HttpResponseMessage response)
        => SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToArray());

    private static async Task<JsonObject> JsonAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    private sealed class LogCollector : ILogEventSink
    {
        internal ConcurrentQueue<string> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture) + logEvent.Exception + string.Join(",", logEvent.Properties));
    }
}
