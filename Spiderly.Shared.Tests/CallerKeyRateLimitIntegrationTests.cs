using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Constants;
using Spiderly.Shared.Extensions;
using Xunit;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins that both limiters <see cref="StartupExtensions.SpiderlyAddRateLimiters"/> registers count an
    /// anonymous caller by <see cref="Helpers.Helper.GetCallerKey"/>, through the real middleware: a second
    /// address in the caller's IPv6 /64 is refused once the first used the limit.
    /// </summary>
    public class CallerKeyRateLimitIntegrationTests
    {
        private const string ClientAddressHeader = "X-Test-Client-Address";

        private static TestServer BuildServer(Settings settings)
        {
            IWebHostBuilder builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.SpiderlyAddRateLimiters(settings);
                })
                .Configure(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ctx.Request.Headers[ClientAddressHeader]!);
                        await next();
                    });
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/", () => "ok");
                        endpoints.MapGet("/upload", () => "ok").RequireRateLimiting(SpiderlyRateLimitPolicies.BlobUpload);
                    });
                });

            return new TestServer(builder);
        }

        private static async Task<HttpStatusCode> GetFrom(TestServer server, string path, string address)
        {
            HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add(ClientAddressHeader, address);
            HttpResponseMessage response = await server.CreateClient().SendAsync(request);
            return response.StatusCode;
        }

        [Fact]
        public async Task The_global_limit_counts_one_ipv6_64_as_one_caller()
        {
            using TestServer server = BuildServer(new Settings { RequestsLimitNumber = 1, RequestsLimitWindow = 60 });

            Assert.Equal(HttpStatusCode.OK, await GetFrom(server, "/", "2001:db8:1:2::1"));
            Assert.Equal(HttpStatusCode.TooManyRequests, await GetFrom(server, "/", "2001:db8:1:2::2"));
            Assert.Equal(HttpStatusCode.OK, await GetFrom(server, "/", "2001:db8:1:3::1"));
        }

        [Fact]
        public async Task The_blob_upload_limit_counts_one_ipv6_64_as_one_caller()
        {
            using TestServer server = BuildServer(new Settings { BlobUploadRequestsLimitNumber = 1, BlobUploadRequestsLimitWindow = 60 });

            Assert.Equal(HttpStatusCode.OK, await GetFrom(server, "/upload", "2001:db8:1:2::1"));
            Assert.Equal(HttpStatusCode.TooManyRequests, await GetFrom(server, "/upload", "2001:db8:1:2::2"));
            Assert.Equal(HttpStatusCode.OK, await GetFrom(server, "/upload", "2001:db8:1:3::1"));
        }
    }
}
