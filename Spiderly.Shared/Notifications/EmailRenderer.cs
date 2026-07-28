using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Renders email content for a notification at <b>send time</b>, as an alternative to the notification's
    /// self-contained <see cref="IEmailNotification.ToEmail"/>. Implementations are DI services, so they can inject
    /// a <c>DbContext</c> and load <b>fresh</b> data (async) — use this for data-rich notifications (e.g. an order
    /// confirmation) where a frozen snapshot in the outbox row would go stale.
    ///
    /// <para><see cref="EmailChannel"/> prefers a registered renderer whose <see cref="NotificationType"/> matches
    /// the notification; if none is registered it falls back to <see cref="IEmailNotification.ToEmail"/>. Register
    /// one per notification type: <c>services.AddScoped&lt;IEmailRenderer, OrderConfirmedEmailRenderer&gt;()</c>.</para>
    /// </summary>
    public interface IEmailRenderer
    {
        /// <summary>The notification type this renderer handles.</summary>
        Type NotificationType { get; }

        /// <summary>
        /// Builds the email content from the (rebuilt) notification, loading fresh data as needed. Return
        /// <c>null</c> to skip sending (e.g. the referenced entity no longer exists). <paramref name="recipient"/>
        /// is <c>null</c> for admin/static-config sends.
        /// </summary>
        Task<EmailContent?> RenderAsync(INotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Typed convenience base for <see cref="IEmailRenderer"/> — handles the cast and <see cref="NotificationType"/>.
    /// </summary>
    /// <typeparam name="TNotification">The notification type this renderer handles.</typeparam>
    public abstract class EmailRenderer<TNotification> : IEmailRenderer
        where TNotification : INotification
    {
        /// <inheritdoc/>
        public Type NotificationType => typeof(TNotification);

        /// <inheritdoc/>
        public Task<EmailContent?> RenderAsync(INotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken)
            => RenderAsync((TNotification)notification, recipient, cancellationToken);

        /// <summary>Builds the email content; return <c>null</c> to skip sending. <paramref name="recipient"/> is <c>null</c> for admin/static-config sends.</summary>
        protected abstract Task<EmailContent?> RenderAsync(TNotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken);
    }
}
