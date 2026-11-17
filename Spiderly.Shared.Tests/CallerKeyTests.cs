using Spiderly.Shared.Helpers;
using Xunit;

namespace Spiderly.Shared.Tests
{
    /// <summary>Pins <see cref="Helper.GetCallerKey"/>, whose summary says why a key is an IPv6 /64.</summary>
    public class CallerKeyTests
    {
        [Fact]
        public void Two_addresses_in_one_ipv6_64_give_one_key()
        {
            string first = Helper.GetCallerKey(TestHttpContexts.From("2001:db8:1:2::1"));
            string second = Helper.GetCallerKey(TestHttpContexts.From("2001:db8:1:2:ffff:ffff:ffff:fffe"));

            Assert.Equal("2001:db8:1:2::/64", first);
            Assert.Equal(first, second);
        }

        [Fact]
        public void An_ipv4_address_is_its_own_key()
        {
            Assert.Equal("203.0.113.7", Helper.GetCallerKey(TestHttpContexts.From("203.0.113.7")));
        }

        [Fact]
        public void An_ipv4_mapped_ipv6_address_gives_its_ipv4_key()
        {
            // A dual-stack socket reports an IPv4 client in this form.
            Assert.Equal("203.0.113.7", Helper.GetCallerKey(TestHttpContexts.From("::ffff:203.0.113.7")));
        }

        [Fact]
        public void Addresses_in_two_ipv6_64s_give_two_keys()
        {
            Assert.NotEqual(
                Helper.GetCallerKey(TestHttpContexts.From("2001:db8:1:2::1")),
                Helper.GetCallerKey(TestHttpContexts.From("2001:db8:1:3::1")));
        }
    }
}
