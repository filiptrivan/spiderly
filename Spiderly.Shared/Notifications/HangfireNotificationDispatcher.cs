using Hangfire;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    public class HangfireNotificationDispatcher : INotificationDispatcher
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public HangfireNotificationDispatcher(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }

        public void DispatchUnhandledException(long? userId, Exception ex)
        {
            if (!Helper.ShouldSendNotification(ex))
                return;

            _backgroundJobClient.Enqueue<UnhandledExceptionNotificationJob>(
                j => j.SendAsync(userId, ex.ToString())
            );
        }

        public void DispatchSecurityEvent(string eventType, string debounceKey, string message)
        {
            if (!Helper.ShouldSendNotification(debounceKey))
                return;

            _backgroundJobClient.Enqueue<SecurityEventNotificationJob>(
                j => j.SendAsync(eventType, message)
            );
        }
    }
}
