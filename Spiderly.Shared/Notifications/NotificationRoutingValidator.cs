using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Fails fast at startup if any notification route points at a channel code that has no registered
    /// <see cref="INotificationChannel"/>. Without this guard a routed-but-unbacked code is silent: the framework's
    /// own <c>UnhandledException</c> / <c>SecurityEvent</c> / <c>JobFailed</c> alerts default to the Email channel,
    /// which is only registered when emailing is enabled — so a consumer that calls <c>AddNotifications</c> without
    /// emailing would have <see cref="DefaultNotificationRouter"/> return no channels and the "always reach admins"
    /// alerts would vanish with no log or throw. Registered as a hosted service so the check runs once, after the
    /// container is built (channels are scoped and may be registered after <c>AddNotifications</c>).
    ///
    /// <para>Also fails fast if more than one <see cref="INotificationRecipientResolver"/> is registered: delivery
    /// resolves a recipient by a single id with no recipient-kind discriminator, so only the first resolver would
    /// ever be used and the rest would silently never match. One resolver is the supported shape.</para>
    /// </summary>
    public class NotificationRoutingValidator : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NotificationRoutingMap _routingMap;

        /// <summary>Creates the validator over the scope factory (channels are scoped) and the routing map.</summary>
        public NotificationRoutingValidator(IServiceScopeFactory scopeFactory, NotificationRoutingMap routingMap)
        {
            _scopeFactory = scopeFactory;
            _routingMap = routingMap;
        }

        /// <summary>Validates that every routed channel code resolves to a registered channel; throws if not, aborting startup.</summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            HashSet<string> registeredCodes = scope.ServiceProvider
                .GetServices<INotificationChannel>()
                .Select(channel => channel.Code)
                .ToHashSet();

            List<string> errors = new();
            foreach (KeyValuePair<Type, List<string>> route in _routingMap.Routes)
            {
                foreach (string code in route.Value.Distinct())
                {
                    if (!registeredCodes.Contains(code))
                        errors.Add($"  - {route.Key.Name} routes to channel '{code}', but no INotificationChannel with that Code is registered.");
                }
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(
                    "Notification routing is misconfigured — every routed channel code must have a registered INotificationChannel:"
                    + Environment.NewLine + string.Join(Environment.NewLine, errors)
                    + Environment.NewLine
                    + "Register the channel (the built-in Email channel requires emailing, e.g. spiderly.AddBrevoEmailing() / spiderly.AddEmailing<T>()) or remove the route.");

            int resolverCount = scope.ServiceProvider.GetServices<INotificationRecipientResolver>().Count();
            if (resolverCount > 1)
                throw new InvalidOperationException(
                    $"{resolverCount} INotificationRecipientResolver implementations are registered, but notification "
                    + "delivery only ever uses one (recipients are resolved by a single id, with no recipient-kind "
                    + "discriminator). Register exactly one resolver.");

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
