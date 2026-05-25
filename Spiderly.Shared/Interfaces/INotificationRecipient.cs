namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Marker for "something a notification can be sent to" (typically the app's user/customer entity).
    /// A recipient supplies its address for a given channel by also implementing that channel's recipient
    /// capability interface — e.g. an Email channel ships <c>IEmailRecipient { string EmailAddress { get; } }</c>,
    /// and a channel a recipient does not implement simply skips that recipient.
    ///
    /// <para>This mirrors the content side (a notification implements each channel's content interface), so a
    /// custom channel ships <b>both</b> its content and recipient interfaces with no framework change. Admin
    /// notifications (<see cref="INotifier.NotifyAdmins"/>) carry no recipient and use the channel's configured
    /// static address instead.</para>
    /// </summary>
    public interface INotificationRecipient
    {
        /// <summary>
        /// Stable id used to persist and later reload this recipient (e.g. the user's <c>Id</c>). When a notification
        /// is delivered asynchronously, only this id is stored; an <see cref="INotificationRecipientResolver"/>
        /// reloads the recipient at delivery time. (<see cref="INotifier.NotifyAdmins"/> carries no recipient, so
        /// this is never read for admin notifications.)
        /// </summary>
        long NotificationRecipientId { get; }
    }
}
