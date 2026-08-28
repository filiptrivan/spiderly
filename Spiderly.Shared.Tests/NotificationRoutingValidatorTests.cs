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

        // The inverse direction. A registered renderer whose notification type is unrouted is dead
        // code that looks alive end to end: staging writes its outbox row, the caller "sends", and
        // Notifier.Dispatch drops it on an empty channel list. A consumer shipped two customer
        // emails that way and nothing anywhere went red.
        [Fact]
        public async Task Throws_when_a_registered_renderer_has_no_route()
        {
            NotificationRoutingValidator validator = new(
                new FakeScopeFactory(
                    channels: new[] { Channel("Email") },
                    renderers: new[] { Renderer<TestNote>() }),
                Map()); // nothing routed

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(default));

            Assert.Contains(nameof(TestNote), exception.Message);
        }

        [Fact]
        public async Task Passes_when_every_registered_renderer_is_routed()
        {
            NotificationRoutingValidator validator = new(
                new FakeScopeFactory(
                    channels: new[] { Channel("Email") },
                    renderers: new[] { Renderer<TestNote>() }),
                Map((typeof(TestNote), "Email")));

            await validator.StartAsync(default); // does not throw
        }

        // ---- helpers ----

        private static NotificationRoutingMap Map(params (Type type, string code)[] routes)
        {
            Dictionary<Type, List<string>> map = new();
            foreach ((Type type, string code) in routes)
            {
                if (!map.TryGetValue(type, out List<string>? codes))
                    map[type] = codes = new List<string>();
                codes.Add(code);
            }
            return new NotificationRoutingMap(map);
        }

        private static INotificationChannel Channel(string code) => new StubChannel(code);
        private static INotificationRecipientResolver Resolver() => new StubResolver();
        private static IEmailRenderer Renderer<TNotification>() where TNotification : INotification
            => new StubRenderer(typeof(TNotification));

        // Hands the validator its channels/resolvers/renderers when it calls GetServices<T>() inside the scope it creates.
        private sealed class FakeScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
        {
            private readonly IEnumerable<INotificationChannel> _channels;
            private readonly IEnumerable<INotificationRecipientResolver> _resolvers;
            private readonly IEnumerable<IEmailRenderer> _renderers;

            public FakeScopeFactory(
                IEnumerable<INotificationChannel> channels,
                IEnumerable<INotificationRecipientResolver>? resolvers = null,
                IEnumerable<IEmailRenderer>? renderers = null)
            {
                _channels = channels;
                _resolvers = resolvers ?? Array.Empty<INotificationRecipientResolver>();
                _renderers = renderers ?? Array.Empty<IEmailRenderer>();
            }

            public IServiceScope CreateScope() => this;
            public IServiceProvider ServiceProvider => this;
            public void Dispose() { }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IEnumerable<INotificationChannel>)) return _channels;
                if (serviceType == typeof(IEnumerable<INotificationRecipientResolver>)) return _resolvers;
                if (serviceType == typeof(IEnumerable<IEmailRenderer>)) return _renderers;
                return null;
            }
        }

        private sealed class StubChannel : INotificationChannel
        {
            public StubChannel(string code) => Code = code;
            public string Code { get; }
            public bool IsConfigured => true;
            public Task SendAsync(INotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        private sealed class StubRenderer : IEmailRenderer
        {
            public StubRenderer(Type notificationType) => NotificationType = notificationType;
            public Type NotificationType { get; }
            public Task<EmailContent?> RenderAsync(INotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken)
                => Task.FromResult<EmailContent?>(null);
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
