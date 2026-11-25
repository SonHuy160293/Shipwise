using System.Buffers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shipwise.Identity.API.Configuration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SessionBodyLimitAttribute : Attribute, IAsyncResourceFilter
{
    private const int Limit = 16 * 1024;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > Limit)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
            return;
        }

        // Bodyless actions never consume the stream themselves. Bound chunked uploads before any command runs.
        if (request.ContentLength is null)
        {
            request.EnableBuffering();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Limit + 1);
            try
            {
                int total = 0;
                int read;
                do
                {
                    read = await request.Body.ReadAsync(buffer.AsMemory(total, Limit + 1 - total), context.HttpContext.RequestAborted);
                    total += read;
                }
                while (read > 0 && total <= Limit);

                if (total > Limit)
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
                    return;
                }

                request.Body.Position = 0;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        await next();
    }
}
