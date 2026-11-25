using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shipwise.AspNetCore.Middleware;
using Shipwise.AspNetCore.Options;
using Shipwise.AspNetCore.Security;
using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class OriginCheckTests
{
    [Theory]
    [InlineData("POST", true, false, null, false)]
    [InlineData("PATCH", false, true, "https://foreign.test", false)]
    [InlineData("DELETE", false, true, "https://app.test", true)]
    [InlineData("PUT", true, false, "https://app.test", true)]
    [InlineData("POST", false, false, null, true)]
    [InlineData("GET", true, true, null, true)]
    public async Task UnsafeMarkedOrCookieRequestsRequireOneAllowedOrigin(string method, bool marked, bool cookie, string? origin, bool allowed)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Method = method;
        ctx.Request.Headers.Authorization = "Bearer test";
        if (marked)
        {
            ctx.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new RequireAllowedOriginAttribute()), "test"));
        }

        if (cookie)
        {
            ctx.Request.Headers.Cookie = "shipwise-refresh=test";
        }

        if (origin is not null)
        {
            ctx.Request.Headers.Origin = origin;
        }

        bool called = false;
        var middleware = new OriginCheckMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(ctx, new(false), Options.Create(new CorsOptions { AllowedOrigins = ["https://app.test"] }));
        Assert.Equal(allowed, called);
        Assert.Equal(allowed ? 200 : 403, ctx.Response.StatusCode);
    }
}
