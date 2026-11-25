using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shipwise.AspNetCore.Options;
using Shipwise.AspNetCore.Security;
using Shipwise.SharedKernel.Abstractions.Shared;

namespace Shipwise.AspNetCore.Middleware;

public sealed class OriginCheckMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuthCookieOptions cookies, IOptions<CorsOptions> cors)
    {
        var request = context.Request;
        bool unsafeMethod = HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method) || HttpMethods.IsDelete(request.Method);
        if (unsafeMethod && (context.GetEndpoint()?.Metadata.GetMetadata<RequireAllowedOriginAttribute>() is not null
            || request.Cookies.ContainsKey(cookies.AccessName) || request.Cookies.ContainsKey(cookies.RefreshName)))
        {
            var origins = request.Headers.Origin;
            if (origins.Count != 1 || !cors.Value.AllowedOrigins.Contains(origins[0], StringComparer.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                var error = new ApiErrorResponse("Request.OriginRejected", "An allowed origin is required.", TraceId: context.TraceIdentifier);
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(error), context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
