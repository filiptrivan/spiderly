using Hangfire;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    public class HangfireExceptionNotificationDispatcher : IExceptionNotificationDispatcher
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public HangfireExceptionNotificationDispatcher(IBackgroundJobClient backgroundJobClient)
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
    }
}
