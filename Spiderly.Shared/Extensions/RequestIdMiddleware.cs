using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Extensions;

public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Response.Headers.ContainsKey("X-Request-Id"))
        {
            string requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault()
                ?? Guid.NewGuid().ToString("N")[..12];

            context.TraceIdentifier = requestId;
            context.Response.Headers["X-Request-Id"] = requestId;
        }

        await _next(context);
    }
}
