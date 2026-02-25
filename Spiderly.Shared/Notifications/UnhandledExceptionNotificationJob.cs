using Hangfire;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Notifications
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public class UnhandledExceptionNotificationJob
    {
        public async Task SendAsync(long? userId, bool isProduction, string exceptionString)
        {
            await Helper.SendUnhandledExceptionNotificationsAsync(userId, isProduction, exceptionString);
        }
    }
}
