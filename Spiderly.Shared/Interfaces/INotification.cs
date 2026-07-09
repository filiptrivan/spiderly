using Spiderly.Shared.Enums;

namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// A notification is a self-contained, strongly-typed data object describing something that happened.
    /// It opts into the channels it supports by also implementing each channel's content interface
    /// (e.g. an Email channel ships <c>IEmailNotification { EmailContent ToEmail(); }</c>); a channel a
    /// notification does not implement simply skips it. The notification carries all data its content
    /// methods need, so it can be serialized to the outbox and rebuilt at delivery.
    ///
    /// <para>Most notifications implement just this (taking the default) plus one or more channel content
    /// interfaces. Override <see cref="Delivery"/> only for must-not-lose messages.</para>
    /// </summary>
    public interface INotification
    {
        /// <summary>
        /// Durability guarantee for this notification. Defaults to <see cref="NotificationDelivery.FireNow"/>;
        /// override to <see cref="NotificationDelivery.Outbox"/> for messages that must not be lost or sent for
        /// a rolled-back transaction.
        /// </summary>
        NotificationDelivery Delivery => NotificationDelivery.FireNow;
    }
}
