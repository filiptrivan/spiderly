using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Notifications
{
    // Sends a Telegram notification when a Hangfire job enters FailedState
    // after all retries are exhausted. Uses IApplyStateFilter so it only fires
    // on the final applied state (AutomaticRetryAttribute redirects intermediate
    // failures to ScheduledState before this filter runs).
    public class HangfireFailedJobNotificationFilter : IApplyStateFilter
    {
        private readonly ILogger<HangfireFailedJobNotificationFilter> _logger;

        public HangfireFailedJobNotificationFilter(ILogger<HangfireFailedJobNotificationFilter> logger)
        {
            _logger = logger;
        }

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (context.NewState is not FailedState failedState)
                return;

            if (!Helper.IsTelegramConfigured())
                return;

            if (!Helper.ShouldSendNotification(failedState.Exception))
                return;

            string jobType = context.BackgroundJob.Job?.Type?.Name ?? "Unknown";
            string jobMethod = context.BackgroundJob.Job?.Method?.Name ?? "Unknown";
            string jobId = context.BackgroundJob.Id;

            string text = $"""
[{SettingsProvider.Current.ApplicationName}] Hangfire Job Failed
Job: {jobType}.{jobMethod} (ID: {jobId})
{failedState.Exception}
""";

            _ = Task.Run(async () =>
            {
                try
                {
                    await Helper.SendTelegramNotificationAsync(text, _logger);
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
