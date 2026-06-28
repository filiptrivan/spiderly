using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Operational notification for a Hangfire job that exhausted its retries and landed in the failed state,
    /// sent to admins via <see cref="INotifier.NotifyAdmins"/>. Delivered <c>FireNow</c> and deduped on the
    /// exception so a repeatedly-failing job alerts at most once per window.
    /// </summary>
    [OutboxCode("Spiderly.JobFailed")]
    public class JobFailedNotification : INotification, IEmailNotification
    {
        /// <summary>Parameterless ctor for deserialization at delivery time.</summary>
        public JobFailedNotification() { }

        /// <summary>Creates the notification.</summary>
        public JobFailedNotification(string jobType, string jobMethod, string jobId, string exceptionString)
        {
            JobType = jobType;
            JobMethod = jobMethod;
            JobId = jobId;
            ExceptionString = exceptionString;
        }

        /// <summary>The failed job's declaring type name.</summary>
        public string JobType { get; set; }

        /// <summary>The failed job's method name.</summary>
        public string JobMethod { get; set; }

        /// <summary>The Hangfire job id (for cross-referencing the dashboard).</summary>
        public string JobId { get; set; }

        /// <summary>The exception that failed the job.</summary>
        public string ExceptionString { get; set; }

        /// <inheritdoc/>
        public string DedupeKey => $"job-failed:{ExceptionString?.GetHashCode()}";

        /// <inheritdoc/>
        public EmailContent ToEmail()
            => new($"Hangfire Job Failed: {JobType}.{JobMethod}",
                   $"Job: {JobType}.{JobMethod} (ID: {JobId})<br>{ExceptionString}");
    }
}
