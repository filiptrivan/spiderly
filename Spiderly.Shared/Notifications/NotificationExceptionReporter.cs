using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Default <see cref="IExceptionReporter"/> — reports unhandled exceptions to admins via the notification
    /// framework (<see cref="INotifier.NotifyAdmins"/> → <see cref="UnhandledExceptionNotification"/> → Email).
    /// Registered by <c>AddNotifications</c>; self-disabling when no operator recipients are configured (the Email
    /// channel no-ops). Add another reporter (e.g. one calling <c>SentrySdk.CaptureException</c>) or replace this to
    /// change where unhandled exceptions go.
    /// </summary>
    public class NotificationExceptionReporter : IExceptionReporter
    {
        private readonly INotifier _notifier;

        public NotificationExceptionReporter(INotifier notifier)
        {
            _notifier = notifier;
        }

        public void Report(ExceptionReport report)
            => _notifier.NotifyAdmins(new UnhandledExceptionNotification(report.UserId, report.Exception.ToString()));
    }
}
