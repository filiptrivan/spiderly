using System.Net;
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Helpers;
using Xunit;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins <see cref="Helper.GetCallerKey"/>. A host usually gets a whole IPv6 /64, so a key per full
    /// IPv6 address would give one host 2^64 separate rate limits.
    /// </summary>
    public class CallerKeyTests
    {
        private static DefaultHttpContext ContextFrom(string ip)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            return httpContext;
        }

        [Fact]
        public void Two_addresses_in_one_ipv6_64_give_one_key()
        {
            string first = Helper.GetCallerKey(ContextFrom("2001:db8:1:2::1"));
            string second = Helper.GetCallerKey(ContextFrom("2001:db8:1:2:ffff:ffff:ffff:fffe"));

            Assert.Equal("2001:db8:1:2::/64", first);
            Assert.Equal(first, second);
        }

        [Fact]
        public void An_ipv4_address_is_its_own_key()
        {
            Assert.Equal("203.0.113.7", Helper.GetCallerKey(ContextFrom("203.0.113.7")));
        }

        [Fact]
        public void An_ipv4_mapped_ipv6_address_gives_its_ipv4_key()
        {
            // A dual-stack socket reports an IPv4 client in this form.
            Assert.Equal("203.0.113.7", Helper.GetCallerKey(ContextFrom("::ffff:203.0.113.7")));
        }

        [Fact]
        public void Addresses_in_two_ipv6_64s_give_two_keys()
        {
            Assert.NotEqual(
                Helper.GetCallerKey(ContextFrom("2001:db8:1:2::1")),
                Helper.GetCallerKey(ContextFrom("2001:db8:1:3::1")));
        }
    }
}
