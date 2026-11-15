using Microsoft.AspNetCore.Http;
using Spiderly.Shared;
using Spiderly.Shared.RateLimiting;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // Pins the global rate-limiter's three-way partition decision: trusted first-party infra beats an
    // authenticated api-key principal beats the caller key (Helper.GetCallerKey), each with its own
    // budget. The trusted class is opt-in — a null detector must leave api-key/caller-key behavior
    // exactly as it was.
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

        private static void WithApiKeyPrincipal(DefaultHttpContext httpContext, string keyId) =>
            httpContext.User = TestPrincipals.ApiKey(keyId);

        [Fact]
        public void No_detector_and_anonymous_yields_caller_key_partition()
        {
            GlobalRateLimitPartition partition =
                GlobalRateLimitPartitioner.Resolve(TestHttpContexts.From("1.2.3.4"), Budgets, trustedCallerDetector: null);

            Assert.Equal("1.2.3.4", partition.Key);
            Assert.Equal(240, partition.PermitLimit);
            Assert.Equal(60, partition.WindowSeconds);
        }

        [Fact]
        public void Api_key_principal_yields_per_key_partition()
        {
            DefaultHttpContext httpContext = TestHttpContexts.From("1.2.3.4");
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
                TestHttpContexts.From("9.9.9.9"), Budgets, new StubTrustedCallerDetector(trusted: true));

            Assert.Equal("trusted:9.9.9.9", partition.Key);
            Assert.Equal(6000, partition.PermitLimit);
            Assert.Equal(30, partition.WindowSeconds);
        }

        [Fact]
        public void Anonymous_callers_in_one_ipv6_64_share_one_partition()
        {
            GlobalRateLimitPartition first = GlobalRateLimitPartitioner.Resolve(
                TestHttpContexts.From("2001:db8:1:2::1"), Budgets, trustedCallerDetector: null);
            GlobalRateLimitPartition second = GlobalRateLimitPartitioner.Resolve(
                TestHttpContexts.From("2001:db8:1:2::2"), Budgets, trustedCallerDetector: null);

            Assert.Equal("2001:db8:1:2::/64", first.Key);
            Assert.Equal(first.Key, second.Key);
        }

        [Fact]
        public void Untrusted_detector_falls_through_to_the_caller_key()
        {
            // A registered detector that returns false must not disturb the anonymous caller-key path.
            GlobalRateLimitPartition partition = GlobalRateLimitPartitioner.Resolve(
                TestHttpContexts.From("1.2.3.4"), Budgets, new StubTrustedCallerDetector(trusted: false));

            Assert.Equal("1.2.3.4", partition.Key);
            Assert.Equal(240, partition.PermitLimit);
        }

        [Fact]
        public void Null_remote_ip_still_yields_a_usable_partition_key()
        {
            // RemoteIpAddress is null on non-socket transports — a Unix-domain-socket Kestrel behind nginx,
            // and any in-memory TestServer. A null partition key is not something the limiter accepts, so
            // this is the difference between "over-throttled" and "every request 500s".
            GlobalRateLimitPartition partition = GlobalRateLimitPartitioner.Resolve(
                new DefaultHttpContext(), Budgets, trustedCallerDetector: null);

            Assert.Equal("unknown", partition.Key);
            Assert.Equal(240, partition.PermitLimit);
        }

        [Fact]
        public void Null_remote_ip_still_yields_a_usable_trusted_partition_key()
        {
            // Interpolation hides the null here rather than crashing, which is worse: "trusted:" silently
            // collapses every trusted caller into one bucket instead of failing where anyone would notice.
            GlobalRateLimitPartition partition = GlobalRateLimitPartitioner.Resolve(
                new DefaultHttpContext(), Budgets, new StubTrustedCallerDetector(trusted: true));

            Assert.Equal("trusted:unknown", partition.Key);
        }

        [Fact]
        public void Trusted_class_wins_over_api_key_principal()
        {
            // A request that is both marked trusted AND carries an api-key principal takes the trusted
            // (most-privileged, first-party) class.
            DefaultHttpContext httpContext = TestHttpContexts.From("9.9.9.9");
            WithApiKeyPrincipal(httpContext, "5");

            GlobalRateLimitPartition partition =
                GlobalRateLimitPartitioner.Resolve(httpContext, Budgets, new StubTrustedCallerDetector(trusted: true));

            Assert.Equal("trusted:9.9.9.9", partition.Key);
            Assert.Equal(6000, partition.PermitLimit);
        }
    }
}
