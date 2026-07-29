using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.RateLimiting;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // End-to-end wiring test for the trusted-caller class: proves the global limiter actually routes a
    // trusted request into the large bucket (never 429) while an untrusted one hits the low per-IP cap.
    // Complements GlobalRateLimitPartitionerTests (which pins the pure decision) by exercising the real
    // AddRateLimiter/UseRateLimiter middleware through a TestServer — the 5 lines of closure glue the
    // unit test can't reach.
    public class TrustedCallerRateLimitIntegrationTests
    {
        private static TestServer BuildServer(bool trusted) => BuildServer(trusted, stampClientIp: true);

        private static TestServer BuildServer(bool trusted, bool stampClientIp)
        {
            Settings settings = new()
            {
                RequestsLimitNumber = 2, // tiny per-IP cap so a handful of requests trips it
                RequestsLimitWindow = 60,
                TrustedRequestsLimitNumber = 1000,
                TrustedRequestsLimitWindow = 60,
            };

            IWebHostBuilder builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.SpiderlyAddRateLimiters(settings);
                    services.AddSingleton<ITrustedCallerDetector>(new StubTrustedCallerDetector(trusted));
                })
                .Configure(app =>
                {
                    // Pin a stable, non-null client IP so the per-IP partition key is deterministic
                    // (TestServer leaves RemoteIpAddress null, which a partition key can't be).
                    if (stampClientIp)
                    {
                        app.Use(async (ctx, next) =>
                        {
                            ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
                            await next();
                        });
                    }
                    app.UseRateLimiter();
                    app.Run(ctx => ctx.Response.WriteAsync("ok"));
                });

            return new TestServer(builder);
        }

        private static async Task<int> CountAccepted(TestServer server, int requests)
        {
            HttpClient client = server.CreateClient();
            int accepted = 0;
            for (int i = 0; i < requests; i++)
            {
                HttpResponseMessage res = await client.GetAsync("/");
                if (res.StatusCode != HttpStatusCode.TooManyRequests) accepted++;
            }
            return accepted;
        }

        [Fact]
        public async Task Untrusted_requests_are_throttled_by_the_low_per_ip_cap()
        {
            using TestServer server = BuildServer(trusted: false);

            int accepted = await CountAccepted(server, requests: 5);

            // PermitLimit = 2, so at most the first couple pass and the rest are 429.
            Assert.True(accepted <= 2, $"expected <= 2 accepted, got {accepted}");
        }

        [Fact]
        public async Task Trusted_requests_bypass_the_low_cap()
        {
            using TestServer server = BuildServer(trusted: true);

            int accepted = await CountAccepted(server, requests: 5);

            // Trusted bucket is 1000, so every request is admitted.
            Assert.Equal(5, accepted);
        }

        [Fact]
        public async Task Requests_with_no_client_ip_are_served_rather_than_faulting()
        {
            // The IP-stamping middleware the other tests use is a workaround for exactly this: without it
            // the partition key is null, which the limiter rejects. A Unix-domain-socket deployment behind
            // nginx has the same shape in production, where the whole app would 500 on every request.
            using TestServer server = BuildServer(trusted: false, stampClientIp: false);

            HttpResponseMessage response = await server.CreateClient().GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
