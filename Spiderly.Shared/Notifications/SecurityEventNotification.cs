using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Operational notification for a security event (e.g. a security-violation exception, a rate-limit rejection),
    /// sent to admins via <see cref="INotifier.NotifyAdmins"/>. Delivered <c>FireNow</c> and deduped on
    /// event type + debounce key.
    /// </summary>
    [OutboxCode("Spiderly.SecurityEvent")]
    public class SecurityEventNotification : INotification, IEmailNotification
    {
        /// <summary>Parameterless ctor for deserialization at delivery time.</summary>
        public SecurityEventNotification() { }

        /// <summary>Creates the notification.</summary>
        public SecurityEventNotification(string eventType, string debounceKey, string message)
        {
            EventType = eventType;
            DebounceKey = debounceKey;
            Message = message;
        }

        /// <summary>Short event label (e.g. <c>"SecurityViolation"</c>, <c>"Rate Limit Rejection"</c>); used as the email subject.</summary>
        public string EventType { get; set; }

        /// <summary>Per-event key used for dedupe (so repeats of the same event collapse within the window).</summary>
        public string DebounceKey { get; set; }

        /// <summary>The event details.</summary>
        public string Message { get; set; }

        /// <inheritdoc/>
        public string DedupeKey => $"security-event:{EventType}:{DebounceKey}";

        /// <inheritdoc/>
        public EmailContent ToEmail()
            => new(EventType, (Message ?? string.Empty).Replace("\n", "<br>"));
    }
}
