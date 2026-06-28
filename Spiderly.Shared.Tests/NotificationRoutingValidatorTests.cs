using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="NotificationRoutingValidator"/> — the boot-time guard that fails fast when a
    /// route points at a channel code with no registered channel (otherwise the notification is silently dropped),
    /// or when more than one recipient resolver is registered (delivery only ever uses one).
    /// </summary>
    public class NotificationRoutingValidatorTests
    {
        [Fact]
        public async Task Passes_when_every_routed_code_has_a_channel()
        {
            NotificationRoutingValidator validator = new(
                new FakeScopeFactory(channels: new[] { Channel("Email") }),
                Map((typeof(TestNote), "Email")));

            await validator.StartAsync(default); // does not throw
        }

        [Fact]
        public async Task Throws_and_names_the_offender_when_a_routed_code_has_no_channel()
        {
            NotificationRoutingValidator validator = new(
                new FakeScopeFactory(channels: new[] { Channel("Email") }),
                Map((typeof(TestNote), "Telegram")));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => validator.StartAsync(default));

            Assert.Contains("Telegram", ex.Message);
            Assert.Contains(nameof(TestNote), ex.Message);
        }

        [Fact]
        public async Task Throws_when_more_than_one_resolver_is_registered()
        {
            NotificationRoutingValidator validator = new(
                new FakeScopeFactory(
                    channels: new[] { Channel("Email") },
                    resolvers: new[] { Resolver(), Resolver() }),
                Map((typeof(TestNote), "Email")));

            await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(default));
        }

        [Fact]
        public async Task Passes_with_a_single_resolver()
        {
            NotificationRoutingValidator validator = new(
                new FakeScopeFactory(
                    channels: new[] { Channel("Email") },
                    resolvers: new[] { Resolver() }),
                Map((typeof(TestNote), "Email")));

            await validator.StartAsync(default); // does not throw
        }

        // ---- helpers ----

        private static NotificationRoutingMap Map(params (Type type, string code)[] routes)
        {
            Dictionary<Type, List<string>> map = new();
            foreach ((Type type, string code) in routes)
            {
                if (!map.TryGetValue(type, out List<string> codes))
                    map[type] = codes = new List<string>();
                codes.Add(code);
            }
            return new NotificationRoutingMap(map);
        }

        private static INotificationChannel Channel(string code) => new StubChannel(code);
        private static INotificationRecipientResolver Resolver() => new StubResolver();

        // Hands the validator its channels/resolvers when it calls GetServices<T>() inside the scope it creates.
        private sealed class FakeScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
        {
            private readonly IEnumerable<INotificationChannel> _channels;
            private readonly IEnumerable<INotificationRecipientResolver> _resolvers;

            public FakeScopeFactory(
                IEnumerable<INotificationChannel> channels,
                IEnumerable<INotificationRecipientResolver> resolvers = null)
            {
                _channels = channels;
                _resolvers = resolvers ?? Array.Empty<INotificationRecipientResolver>();
            }

            public IServiceScope CreateScope() => this;
            public IServiceProvider ServiceProvider => this;
            public void Dispose() { }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IEnumerable<INotificationChannel>)) return _channels;
                if (serviceType == typeof(IEnumerable<INotificationRecipientResolver>)) return _resolvers;
                return null;
            }
        }

        private sealed class StubChannel : INotificationChannel
        {
            public StubChannel(string code) => Code = code;
            public string Code { get; }
            public bool IsConfigured => true;
            public Task SendAsync(INotification notification, INotificationRecipient recipient, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        private sealed class StubResolver : INotificationRecipientResolver
        {
            public Task<INotificationRecipient> ResolveAsync(long recipientId)
                => Task.FromResult<INotificationRecipient>(null!);
        }

        private sealed class TestNote : INotification
        {
        }
    }
}
