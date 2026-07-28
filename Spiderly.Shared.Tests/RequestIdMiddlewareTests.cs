using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Extensions;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Pins the one-id-namespace contract: the X-Request-Id response header carries the request's W3C trace id —
/// the same id ApiErrorDTO.TraceId shows users and the error tracker indexes — never a parallel GUID, and
/// never a client-supplied value (a caller must not be able to spoof a logged correlation field; upstream
/// correlation is the standard traceparent header, which ASP.NET Core continues natively).
/// </summary>
public class RequestIdMiddlewareTests
{
    private static async Task<DefaultHttpContext> RunAsync(string inboundRequestId = null)
    {
        DefaultHttpContext context = new();
        if (inboundRequestId != null)
            context.Request.Headers["X-Request-Id"] = inboundRequestId;

        await new RequestIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        return context;
    }

    private static void StopAmbientActivities()
    {
        while (Activity.Current != null)
            Activity.Current.Stop();
    }

    [Fact]
    public async Task Response_header_carries_the_current_trace_id()
    {
        using Activity activity = new Activity("test-request").Start();

        DefaultHttpContext context = await RunAsync();

        Assert.Equal(activity.TraceId.ToString(), context.Response.Headers["X-Request-Id"].ToString());
    }

    [Fact]
    public async Task Client_supplied_request_id_is_ignored()
    {
        using Activity activity = new Activity("test-request").Start();

        DefaultHttpContext context = await RunAsync(inboundRequestId: "spoofed-id");

        Assert.Equal(activity.TraceId.ToString(), context.Response.Headers["X-Request-Id"].ToString());
    }

    [Fact]
    public async Task Trace_identifier_is_left_untouched()
    {
        using Activity activity = new Activity("test-request").Start();

        DefaultHttpContext context = await RunAsync(inboundRequestId: "spoofed-id");

        Assert.NotEqual("spoofed-id", context.TraceIdentifier);
    }

    [Fact]
    public async Task Without_an_ambient_activity_the_header_falls_back_to_the_trace_identifier()
    {
        StopAmbientActivities();

        DefaultHttpContext context = await RunAsync();

        Assert.Equal(context.TraceIdentifier, context.Response.Headers["X-Request-Id"].ToString());
    }
}
