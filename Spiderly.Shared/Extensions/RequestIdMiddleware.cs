using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Extensions;

/// <summary>
/// Stamps every response with an <c>X-Request-Id</c> header carrying the request's W3C trace id — the same
/// single correlation id <see cref="DTO.ApiErrorDTO.TraceId"/> shows users and error trackers index (e.g.
/// Sentry's <c>trace:</c> search) — so any response in hand (curl, a partner report, a proxy log) leads
/// straight to the matching server logs and tracker events. Deliberately one id namespace: no generated
/// GUID, and a client-supplied <c>X-Request-Id</c> is ignored (honoring it would let any caller spoof a
/// logged correlation field; upstream callers correlate via the standard <c>traceparent</c> header, which
/// ASP.NET Core continues natively). Falls back to <see cref="HttpContext.TraceIdentifier"/> when no
/// ambient <see cref="System.Diagnostics.Activity"/> exists.
/// </summary>
public class RequestIdMiddleware
{
    /// <summary>The response header carrying the request's correlation (trace) id.</summary>
    public const string HeaderName = "X-Request-Id";

    private readonly RequestDelegate _next;

    /// <summary>Standard middleware constructor.</summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Sets the <c>X-Request-Id</c> response header and passes the request on.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers[HeaderName] =
            TraceCorrelation.CurrentTraceId() ?? context.TraceIdentifier;

        await _next(context);
    }
}
