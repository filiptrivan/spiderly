using Microsoft.Extensions.Options;
using Spiderly.Shared;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Emailing;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="EmailChannel"/> — the capability-interface routing: a notification without
    /// <see cref="IEmailNotification"/> is skipped; <c>Notify</c> reads the recipient's <see cref="IEmailRecipient"/>
    /// address; <c>NotifyAdmins</c> (null recipient) uses the configured operator list; a recipient with no email
    /// address is skipped.
    /// </summary>
    public class EmailChannelTests
    {
        [Fact]
        public async Task Notification_without_email_content_is_skipped()
        {
            FakeEmailingService email = new();

            await NewChannel(email).SendAsync(new NonEmailNotification(), new EmailRecipient("a@b.com"), default);

            Assert.Empty(email.Single);
            Assert.Empty(email.Multi);
        }

        [Fact]
        public async Task Notify_sends_to_the_recipients_email_address()
        {
            FakeEmailingService email = new();

            await NewChannel(email).SendAsync(new EmailableNotification(), new EmailRecipient("a@b.com"), default);

            (string to, string subject, string _) = Assert.Single(email.Single);
            Assert.Equal("a@b.com", to);
            Assert.Equal("Subject", subject);
            Assert.Empty(email.Multi);
        }

        [Fact]
        public async Task NotifyAdmins_sends_to_the_configured_recipients()
        {
            FakeEmailingService email = new();

            await NewChannel(email, "ops1@x.com", "ops2@x.com")
                .SendAsync(new EmailableNotification(), recipient: null, default);

            (List<string> to, string _, string _) = Assert.Single(email.Multi);
            Assert.Equal(new[] { "ops1@x.com", "ops2@x.com" }, to);
            Assert.Empty(email.Single);
        }

        [Fact]
        public async Task Recipient_without_email_address_is_skipped()
        {
            FakeEmailingService email = new();

            await NewChannel(email).SendAsync(new EmailableNotification(), new AddresslessRecipient(), default);

            Assert.Empty(email.Single);
            Assert.Empty(email.Multi);
        }

        [Fact]
        public async Task NotifyAdmins_with_no_configured_recipients_sends_nothing()
        {
            FakeEmailingService email = new();

            await NewChannel(email /* no admin recipients */)
                .SendAsync(new EmailableNotification(), recipient: null, default);

            Assert.Empty(email.Multi);
            Assert.Empty(email.Single);
        }

        [Fact]
        public async Task Renderer_is_preferred_over_self_contained_ToEmail()
        {
            FakeEmailingService email = new();
            FakeEmailRenderer renderer = new(typeof(EmailableNotification), new EmailContent("FromRenderer", "<p>fresh</p>"));

            await NewChannelWithRenderers(email, renderer)
                .SendAsync(new EmailableNotification(), new EmailRecipient("a@b.com"), default);

            (string _, string subject, string _) = Assert.Single(email.Single);
            Assert.Equal("FromRenderer", subject); // the renderer won, not ToEmail()'s "Subject"
        }

        [Fact]
        public async Task Renderer_enables_a_notification_that_has_no_ToEmail()
        {
            FakeEmailingService email = new();
            FakeEmailRenderer renderer = new(typeof(NonEmailNotification), new EmailContent("R", "B"));

            await NewChannelWithRenderers(email, renderer)
                .SendAsync(new NonEmailNotification(), new EmailRecipient("a@b.com"), default);

            Assert.Single(email.Single);
        }

        [Fact]
        public async Task Renderer_returning_null_skips_the_send()
        {
            FakeEmailingService email = new();
            FakeEmailRenderer renderer = new(typeof(EmailableNotification), content: null);

            await NewChannelWithRenderers(email, renderer)
                .SendAsync(new EmailableNotification(), new EmailRecipient("a@b.com"), default);

            Assert.Empty(email.Single);
            Assert.Empty(email.Multi);
        }

        // ---- helpers ----

        private static EmailChannel NewChannel(FakeEmailingService email, params string[] adminRecipients)
            => new(email, Array.Empty<IEmailRenderer>(), Options.Create(new NotificationOptions
            {
                AdminRecipients = adminRecipients.ToList(),
            }));

        private static EmailChannel NewChannelWithRenderers(FakeEmailingService email, params IEmailRenderer[] renderers)
            => new(email, renderers, Options.Create(new NotificationOptions()));

        private sealed class FakeEmailRenderer : IEmailRenderer
        {
            private readonly EmailContent? _content;
            public FakeEmailRenderer(Type notificationType, EmailContent? content)
            {
                NotificationType = notificationType;
                _content = content;
            }
            public Type NotificationType { get; }
            public Task<EmailContent?> RenderAsync(INotification notification, INotificationRecipient recipient, CancellationToken cancellationToken)
                => Task.FromResult(_content);
        }

        private sealed class EmailableNotification : INotification, IEmailNotification
        {
            public EmailContent ToEmail() => new("Subject", "<p>Body</p>");
        }

        private sealed class NonEmailNotification : INotification
        {
        }

        private sealed class EmailRecipient : INotificationRecipient, IEmailRecipient
        {
            public EmailRecipient(string emailAddress) => EmailAddress = emailAddress;
            public long NotificationRecipientId => 1;
            public string EmailAddress { get; }
        }

        private sealed class AddresslessRecipient : INotificationRecipient
        {
            public long NotificationRecipientId => 2;
        }

        private sealed class FakeEmailingService : IEmailingService
        {
            public List<(string To, string Subject, string Body)> Single { get; } = new();
            public List<(List<string> To, string Subject, string Body)> Multi { get; } = new();

            public bool IsConfigured() => true;

            public Task SendEmailAsync(string recipient, string subject, string body, EmailSender? from = null)
            {
                Single.Add((recipient, subject, body));
                return Task.CompletedTask;
            }

            public Task SendEmailAsync(List<string> recipients, string subject, string body)
            {
                Multi.Add((recipients, subject, body));
                return Task.CompletedTask;
            }

            public Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, EmailSender? from = null)
                => throw new NotSupportedException();

            public Task SendEmailFromBackgroundJobAsync(string recipient, string subject, string body)
                => throw new NotSupportedException();

            public Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template)
                => throw new NotSupportedException();
        }
    }
}
