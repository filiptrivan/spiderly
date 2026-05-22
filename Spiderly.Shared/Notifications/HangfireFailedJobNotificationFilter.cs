using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Sends a Telegram notification when a Hangfire job enters FailedState
    /// after all retries are exhausted. Uses IApplyStateFilter so it only fires
    /// on the final applied state (AutomaticRetryAttribute redirects intermediate
    /// failures to ScheduledState before this filter runs).
    /// Registered via <c>app.SpiderlyUseHangfireFailedJobNotificationFilter()</c> after the DI
    /// container is built, so its dependencies can be resolved from the service provider.
    /// </summary>
    public class HangfireFailedJobNotificationFilter : IApplyStateFilter
    {
        private readonly TelegramNotifier _telegramNotifier;
        private readonly NotificationRateLimiter _rateLimiter;
        private readonly NotificationOptions _notificationSettings;
        private readonly ILogger<HangfireFailedJobNotificationFilter> _logger;

        public HangfireFailedJobNotificationFilter(
            TelegramNotifier telegramNotifier,
            NotificationRateLimiter rateLimiter,
            IOptions<NotificationOptions> notificationOptions,
            ILogger<HangfireFailedJobNotificationFilter> logger)
        {
            _telegramNotifier = telegramNotifier;
            _rateLimiter = rateLimiter;
            _notificationSettings = notificationOptions.Value;
            _logger = logger;
        }

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (context.NewState is not FailedState failedState)
                return;

            if (!_telegramNotifier.IsConfigured)
                return;

            if (!_rateLimiter.ShouldSend(failedState.Exception))
                return;

            string jobType = context.BackgroundJob.Job?.Type?.Name ?? "Unknown";
            string jobMethod = context.BackgroundJob.Job?.Method?.Name ?? "Unknown";
            string jobId = context.BackgroundJob.Id;

            string text = $"""
[{_notificationSettings.ApplicationName}] Hangfire Job Failed
Job: {jobType}.{jobMethod} (ID: {jobId})
{failedState.Exception}
""";

            _ = Task.Run(async () =>
            {
                try
                {
                    await _telegramNotifier.SendAsync(text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send Telegram notification for Hangfire job {JobId}.", jobId);
                }
            });
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
        }
    }
}
