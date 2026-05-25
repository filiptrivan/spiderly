using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Operational notification for an unhandled exception, sent to admins via <see cref="INotifier.NotifyAdmins"/>.
    /// Delivered <c>FireNow</c> (the default) and deduped on the exception text so a storm collapses to one alert.
    /// </summary>
    [NotificationCode("Spiderly.UnhandledException")]
    public class UnhandledExceptionNotification : INotification, IEmailNotification
    {
        /// <summary>Parameterless ctor for deserialization at delivery time.</summary>
        public UnhandledExceptionNotification() { }

        /// <summary>Creates the notification.</summary>
        public UnhandledExceptionNotification(long? userId, string exceptionString)
        {
            UserId = userId;
            ExceptionString = exceptionString;
        }

        /// <summary>The authenticated user id at the time of the exception, if any.</summary>
        public long? UserId { get; set; }

        /// <summary>The exception details (typically <c>ex.ToString()</c>).</summary>
        public string ExceptionString { get; set; }

        /// <inheritdoc/>
        public string DedupeKey => $"unhandled-exception:{ExceptionString?.GetHashCode()}";

        /// <inheritdoc/>
        public EmailContent ToEmail()
            => new("Unhandled Exception", $"Currently authenticated user id: {UserId}; <br> {ExceptionString}");
    }
}
