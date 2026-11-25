using Microsoft.Extensions.Options;
using Shipwise.AspNetCore.Options;
using Shipwise.Identity.Application.Features.Auth;

namespace Shipwise.Identity.API.Configuration;

public sealed class AuthCookieWriter(AuthCookieOptions cookies, IOptions<JwtOptions> jwt)
{
    public string? ReadRefresh(HttpRequest request) => request.Cookies[cookies.RefreshName];

    public void Write(HttpResponse response, SignInResult issuance)
    {
        var access = Options("/");
        access.MaxAge = TimeSpan.FromMinutes(jwt.Value.AccessTokenExpirationMinutes);
        response.Cookies.Append(cookies.AccessName, issuance.AccessToken, access);
        if (issuance.RefreshToken is { } token)
        {
            var refresh = Options(cookies.RefreshPath);
            refresh.Expires = issuance.IdleExpiresAt;
            response.Cookies.Append(cookies.RefreshName, token, refresh);
        }
    }

    public void Clear(HttpResponse response)
    {
        foreach (var (name, path) in new[] { (cookies.AccessName, "/"), (cookies.RefreshName, cookies.RefreshPath) })
        {
            var options = Options(path);
            options.MaxAge = TimeSpan.Zero;
            options.Expires = DateTimeOffset.UnixEpoch;
            response.Cookies.Append(name, string.Empty, options);
        }
    }

    private CookieOptions Options(string path)
        => new() { HttpOnly = true, Secure = cookies.Secure, SameSite = SameSiteMode.Strict, Path = path };
}
