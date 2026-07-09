using Microsoft.Extensions.Options;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// The built-in Email notification channel — the one channel Spiderly core ships. Builds content from a
    /// registered <see cref="IEmailRenderer"/> for the notification type (preferred — can load fresh data), falling
    /// back to the notification's self-contained <see cref="IEmailNotification.ToEmail"/>; a notification with
    /// neither is skipped. For <see cref="INotifier.Notify"/> it reads the address from the recipient's
    /// <see cref="IEmailRecipient"/>; for <see cref="INotifier.NotifyAdmins"/> (null recipient) it sends to the
    /// configured operator recipients.
    /// </summary>
    public class EmailChannel : INotificationChannel
    {
        private readonly IEmailingService _emailingService;
        private readonly IEnumerable<IEmailRenderer> _renderers;
        private readonly NotificationOptions _options;

        /// <summary>Creates the Email channel over the app's emailing service, registered renderers, and options.</summary>
        public EmailChannel(
            IEmailingService emailingService,
            IEnumerable<IEmailRenderer> renderers,
            IOptions<NotificationOptions> options)
        {
            _emailingService = emailingService;
            _renderers = renderers;
            _options = options.Value;
        }

        /// <summary>The Email channel code, also exposed as a const so routing config can avoid a magic string.</summary>
        public const string ChannelCode = "Email";

        /// <inheritdoc/>
        public string Code => ChannelCode;

        /// <inheritdoc/>
        public bool IsConfigured => _emailingService.IsConfigured();

        /// <inheritdoc/>
        public async Task SendAsync(INotification notification, INotificationRecipient recipient, CancellationToken cancellationToken)
        {
            EmailContent content = await ResolveContentAsync(notification, recipient, cancellationToken);
            if (content == null)
                return; // no renderer + not an IEmailNotification, or the renderer chose to skip

            if (recipient == null)
            {
                // Admin/static send (NotifyAdmins) → the configured admin recipient list.
                List<string> adminRecipients = _options.AdminRecipients;
                if (adminRecipients != null && adminRecipients.Count > 0)
                    await _emailingService.SendEmailAsync(adminRecipients, content.Subject, content.Body);

                return;
            }

            if (recipient is IEmailRecipient emailRecipient
                && !string.IsNullOrWhiteSpace(emailRecipient.EmailAddress))
            {
                await _emailingService.SendEmailAsync(emailRecipient.EmailAddress, content.Subject, content.Body);
            }
            // else: recipient has no email address — skip
        }

        // Prefer a registered renderer (can load fresh data) over the notification's self-contained ToEmail().
        private async Task<EmailContent> ResolveContentAsync(INotification notification, INotificationRecipient recipient, CancellationToken cancellationToken)
        {
            IEmailRenderer renderer = _renderers.FirstOrDefault(r => r.NotificationType == notification.GetType());
            if (renderer != null)
                return await renderer.RenderAsync(notification, recipient, cancellationToken);

            if (notification is IEmailNotification emailNotification)
                return emailNotification.ToEmail();

            return null;
        }
    }
}
