using Hangfire;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Hangfire job for <see cref="Spiderly.Shared.Enums.NotificationDelivery.FireNow"/> delivery: one enqueued job
    /// per (notification, recipient, channel), so Hangfire's retry isolates a single channel. Thin wrapper over
    /// <see cref="NotificationDeliveryExecutor"/>; arguments are all simple serializable values.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public class NotificationDeliveryJob
    {
        private readonly NotificationDeliveryExecutor _executor;

        /// <summary>Creates the job over the shared delivery executor.</summary>
        public NotificationDeliveryJob(NotificationDeliveryExecutor executor)
        {
            _executor = executor;
        }

        /// <summary>Delivers one notification to one channel. Invoked by Hangfire with serialized arguments.</summary>
        public Task DeliverAsync(string notificationCode, string notificationData, long? recipientId, string channelCode)
            => _executor.DeliverAsync(notificationCode, notificationData, recipientId, channelCode, CancellationToken.None);
    }
}
