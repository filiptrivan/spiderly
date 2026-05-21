using Hangfire;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    public class HangfireNotificationDispatcher : INotificationDispatcher
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly NotificationRateLimiter _rateLimiter;

        public HangfireNotificationDispatcher(IBackgroundJobClient backgroundJobClient, NotificationRateLimiter rateLimiter)
        {
            _backgroundJobClient = backgroundJobClient;
            _rateLimiter = rateLimiter;
        }

        public void DispatchUnhandledException(long? userId, Exception ex)
        {
            if (!_rateLimiter.ShouldSend(ex))
                return;

            _backgroundJobClient.Enqueue<UnhandledExceptionNotificationJob>(
                j => j.SendAsync(userId, ex.ToString())
            );
        }

        public void DispatchSecurityEvent(string eventType, string debounceKey, string message)
        {
            if (!_rateLimiter.ShouldSend(debounceKey))
                return;

            _backgroundJobClient.Enqueue<SecurityEventNotificationJob>(
                j => j.SendAsync(eventType, message)
            );
        }
    }
}
