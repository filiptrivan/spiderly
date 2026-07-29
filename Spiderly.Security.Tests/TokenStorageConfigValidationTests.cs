using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spiderly.Security.Extensions;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// <c>UseRedisCache</c> selects the token-storage backend at composition time, but the multiplexer is
    /// registered as a lazy singleton factory — so a missing <c>RedisConnectionString</c> does not fail at
    /// boot. It fails the first time token storage is resolved, i.e. the first login, on a deploy whose
    /// startup looked completely clean. That is the same shape as the <c>EmailSender</c> misbinding that
    /// sat latent for weeks, and the reason the repo requires a <c>ValidateOnStart</c> guard for config
    /// that is required only when a feature is enabled.
    /// </summary>
    public class TokenStorageConfigValidationTests
    {
        private static IConfiguration Config(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        private static IStartupValidator BuildAndGetValidator(Dictionary<string, string?> values)
        {
            ServiceCollection services = new();
            services.AddSpiderlyTokenStorage(Config(values));
            return services.BuildServiceProvider().GetRequiredService<IStartupValidator>();
        }

        [Fact]
        public void UseRedisCache_withoutAConnectionString_failsAtStartup()
        {
            IStartupValidator validator = BuildAndGetValidator(new()
            {
                ["AppSettings:Spiderly.Security:UseRedisCache"] = "true",
            });

            OptionsValidationException exception = Assert.Throws<OptionsValidationException>(validator.Validate);
            Assert.Contains("RedisConnectionString", string.Join(" ", exception.Failures));
        }

        [Fact]
        public void UseRedisCache_withAnEmptyConnectionString_failsAtStartup()
        {
            IStartupValidator validator = BuildAndGetValidator(new()
            {
                ["AppSettings:Spiderly.Security:UseRedisCache"] = "true",
                ["AppSettings:Spiderly.Security:RedisConnectionString"] = "   ",
            });

            Assert.Throws<OptionsValidationException>(validator.Validate);
        }

        [Fact]
        public void UseRedisCache_withAConnectionString_startsUp()
        {
            // The guard validates the value, not reachability: coupling boot to Redis being up would turn a
            // transient outage into a crash-loop, and StackExchange.Redis reconnects on its own.
            IStartupValidator validator = BuildAndGetValidator(new()
            {
                ["AppSettings:Spiderly.Security:UseRedisCache"] = "true",
                ["AppSettings:Spiderly.Security:RedisConnectionString"] = "localhost:6379",
            });

            validator.Validate();
        }

        [Fact]
        public void InMemoryStorage_needsNoConnectionString()
        {
            IStartupValidator validator = BuildAndGetValidator(new()
            {
                ["AppSettings:Spiderly.Security:UseRedisCache"] = "false",
            });

            validator.Validate();
        }
    }
}
