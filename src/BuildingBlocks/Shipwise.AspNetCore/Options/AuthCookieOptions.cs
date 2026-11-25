namespace Shipwise.AspNetCore.Options;

public sealed class AuthCookieOptions(bool secure)
{
    public bool Secure { get; } = secure;
    public string AccessName => Secure ? "__Host-shipwise-access" : "shipwise-access";
    public string RefreshName => Secure ? "__Secure-shipwise-refresh" : "shipwise-refresh";
    public string RefreshPath { get; } = "/api/v1/auth";
}
