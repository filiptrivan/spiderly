using System.Net;
using Microsoft.AspNetCore.Http;
using Spiderly.Shared;
using Spiderly.Shared.RateLimiting;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // Pins the global rate-limiter's three-way partition decision: trusted first-party infra beats an
    // authenticated api-key principal beats per-IP, each with its own budget. The trusted class is
    // opt-in — a null detector must leave api-key/per-IP behavior exactly as it was.
    public class GlobalRateLimitPartitionerTests
    {
        private static readonly Settings Budgets = new()
        {
            RequestsLimitNumber = 240,
            RequestsLimitWindow = 60,
            ApiKeyRequestsLimitNumber = 1200,
            ApiKeyRequestsLimitWindow = 60,
            TrustedRequestsLimitNumber = 6000,
            TrustedRequestsLimitWindow = 30,
        };

        private static DefaultHttpContext ContextFrom(string ip)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            return httpContext;
        }

        private static void WithApiKeyPrincipal(DefaultHttpContext httpContext, string keyId) =>
            httpContext.User = TestPrincipals.ApiKey(keyId);

        [Fact]
        public void No_detector_and_anonymous_yields_per_ip_partition()
        {
            GlobalRateLimitPartition partition =
                GlobalRateLimitPartitioner.Resolve(ContextFrom("1.2.3.4"), Budgets, trustedCallerDetector: null);

            Assert.Equal("1.2.3.4", partition.Key);
            Assert.Equal(240, partition.PermitLimit);
            Assert.Equal(60, partition.WindowSeconds);
        }

        [Fact]
        public void Api_key_principal_yields_per_key_partition()
        {
            DefaultHttpContext httpContext = ContextFrom("1.2.3.4");
            WithApiKeyPrincipal(httpContext, "5");

            GlobalRateLimitPartition partition =
                GlobalRateLimitPartitioner.Resolve(httpContext, Budgets, trustedCallerDetector: null);

            Assert.Equal("api-key:5", partition.Key);
            Assert.Equal(1200, partition.PermitLimit);
        }

        [Fact]
        public void Trusted_caller_yields_large_per_ip_trusted_partition()
        {
            GlobalRateLimitPartition partition = GlobalRateLimitPartitioner.Resolve(
                ContextFrom("9.9.9.9"), Budgets, new StubTrustedCallerDetector(trusted: true));

            Assert.Equal("trusted:9.9.9.9", partition.Key);
            Assert.Equal(6000, partition.PermitLimit);
            Assert.Equal(30, partition.WindowSeconds);
        }

        [Fact]
        public void Untrusted_detector_falls_through_to_per_ip()
        {
            // A registered detector that returns false must not disturb the anonymous/per-IP path.
            GlobalRateLimitPartition partition = GlobalRateLimitPartitioner.Resolve(
                ContextFrom("1.2.3.4"), Budgets, new StubTrustedCallerDetector(trusted: false));

            Assert.Equal("1.2.3.4", partition.Key);
            Assert.Equal(240, partition.PermitLimit);
        }

        [Fact]
        public void Trusted_class_wins_over_api_key_principal()
        {
            // A request that is both marked trusted AND carries an api-key principal takes the trusted
            // (most-privileged, first-party) class.
            DefaultHttpContext httpContext = ContextFrom("9.9.9.9");
            WithApiKeyPrincipal(httpContext, "5");

            GlobalRateLimitPartition partition =
                GlobalRateLimitPartitioner.Resolve(httpContext, Budgets, new StubTrustedCallerDetector(trusted: true));

            Assert.Equal("trusted:9.9.9.9", partition.Key);
            Assert.Equal(6000, partition.PermitLimit);
        }
    }
}
