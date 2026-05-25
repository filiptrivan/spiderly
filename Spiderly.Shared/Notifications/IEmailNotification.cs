namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Implemented by a notification that can be delivered over the Email channel. This is the Email channel's
    /// <i>content</i> capability interface — a notification opts into email by implementing it; one that does not
    /// is silently skipped by <see cref="EmailChannel"/>.
    /// </summary>
    public interface IEmailNotification
    {
        /// <summary>Renders this notification's email subject + body.</summary>
        EmailContent ToEmail();
    }
}
