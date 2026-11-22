using System.Net;
using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Tests
{
    // Shared builder for the requests the rate-limiter partition tests need, so "a request from this
    // client address" lives in one place instead of being inlined per test.
    internal static class TestHttpContexts
    {
        public static DefaultHttpContext From(string ip)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            return httpContext;
        }
    }
}
