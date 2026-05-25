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
    /// <para>Most notifications implement just this (taking the defaults) plus one or more channel content
    /// interfaces. Override <see cref="Delivery"/> only for must-not-lose messages; set <see cref="DedupeKey"/>
    /// only to debounce repeats.</para>
    /// </summary>
    public interface INotification
    {
        /// <summary>
        /// Durability guarantee for this notification. Defaults to <see cref="NotificationDelivery.FireNow"/>;
        /// override to <see cref="NotificationDelivery.Outbox"/> for messages that must not be lost or sent for
        /// a rolled-back transaction.
        /// </summary>
        NotificationDelivery Delivery => NotificationDelivery.FireNow;

        /// <summary>
        /// Optional debounce key. When non-null, repeats with the same key inside the dedupe window are suppressed
        /// (e.g. collapse an exception storm to one alert). <c>null</c> (the default) disables dedupe — required for
        /// per-recipient customer notifications, which must never be merged.
        /// </summary>
        string DedupeKey => null;

        /// <summary>
        /// Window for <see cref="DedupeKey"/> suppression. <c>null</c> (the default) uses the global
        /// <c>NotificationRateLimitMinutes</c>; set it to give this notification its own window (e.g. a daily
        /// summary at 24h while exceptions stay at the global default). Ignored when <see cref="DedupeKey"/> is null.
        /// </summary>
        TimeSpan? DedupeWindow => null;
    }
}
