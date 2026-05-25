using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Sends a <see cref="JobFailedNotification"/> when a Hangfire job enters <see cref="FailedState"/> after all
    /// retries are exhausted (uses <see cref="IApplyStateFilter"/> so it only fires on the final applied state —
    /// <c>AutomaticRetryAttribute</c> redirects intermediate failures to <see cref="ScheduledState"/> first).
    /// Registered via <c>app.SpiderlyUseHangfireFailedJobNotificationFilter()</c> after the container is built.
    ///
    /// <para>The filter is a singleton, so it resolves the scoped <see cref="INotifier"/> from a fresh DI scope per
    /// failure. Dedupe is handled by the notifier (via <see cref="JobFailedNotification.DedupeKey"/>). It skips
    /// failures of <see cref="NotificationDeliveryJob"/> itself to avoid an alert loop when delivery is broken.</para>
    /// </summary>
    public class HangfireFailedJobNotificationFilter : IApplyStateFilter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireFailedJobNotificationFilter> _logger;

        /// <summary>Creates the filter over the root service provider (scopes are created per failure).</summary>
        public HangfireFailedJobNotificationFilter(
            IServiceProvider serviceProvider,
            ILogger<HangfireFailedJobNotificationFilter> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (context.NewState is not FailedState failedState)
                return;

            // Don't alert on the delivery job's own failures — that would loop (a broken Email channel would
            // fail the delivery job, which would try to deliver another failure notification, and so on).
            if (context.BackgroundJob.Job?.Type == typeof(NotificationDeliveryJob))
                return;

            string jobType = context.BackgroundJob.Job?.Type?.Name ?? "Unknown";
            string jobMethod = context.BackgroundJob.Job?.Method?.Name ?? "Unknown";
            string jobId = context.BackgroundJob.Id;
            string exceptionString = failedState.Exception?.ToString();

            _ = Task.Run(() =>
            {
                try
                {
                    using IServiceScope scope = _serviceProvider.CreateScope();
                    INotifier notifier = scope.ServiceProvider.GetService<INotifier>();
                    notifier?.NotifyAdmins(new JobFailedNotification(jobType, jobMethod, jobId, exceptionString));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send Hangfire job-failed notification for job {JobId}.", jobId);
                }
            });
        }

        /// <inheritdoc/>
        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
        }
    }
}
