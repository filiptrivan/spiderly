using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Spiderly.Shared.Emailing
{
    public class EmailingService : IEmailingService
    {
        private readonly SmtpClient _smtpClient;
        private readonly ILogger<EmailingService> _logger;
        private readonly EmailOptions _emailSettings;
        private readonly IOutboundEmailHeaderProvider? _headerProvider;

        public EmailingService(ILogger<EmailingService> logger, IOptions<EmailOptions> emailOptions, IOutboundEmailHeaderProvider? headerProvider = null)
        {
            _emailSettings = emailOptions.Value;
            _headerProvider = headerProvider;
            _smtpClient = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
            {
                Credentials = new NetworkCredential(_emailSettings.EmailSender?.Email, _emailSettings.EmailSenderPassword),
                EnableSsl = true
            };
            _logger = logger;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_emailSettings.EmailSender?.Email) &&
                   !string.IsNullOrWhiteSpace(_emailSettings.EmailSenderPassword) &&
                   !string.IsNullOrWhiteSpace(_emailSettings.SmtpHost) &&
                   _emailSettings.SmtpPort > 0;
        }

        public async Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template)
        {
            using (MailMessage mailMessage = BuildMessage(toEmail, template.Subject, template.Body))
            {
                await _smtpClient.SendMailAsync(mailMessage); // https://stackoverflow.com/questions/11120350/how-to-check-programmatically-if-an-email-is-existing-or-not
            }
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, EmailSender? from = null, EmailSender? replyTo = null)
        {
            using (MailMessage mailMessage = BuildMessage(recipient, subject, body, from, replyTo))
            {
                await _smtpClient.SendMailAsync(mailMessage);
            }
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, EmailSender? from = null, EmailSender? replyTo = null)
        {
            using MailMessage mailMessage = BuildMessage(recipient, subject, body, from, replyTo);

            if (attachments != null)
            {
                foreach (EmailAttachment attachment in attachments)
                {
                    if (attachment == null || string.IsNullOrEmpty(attachment.ContentBase64))
                        continue;

                    byte[] bytes = Convert.FromBase64String(attachment.ContentBase64);
                    MemoryStream stream = new(bytes);
                    mailMessage.Attachments.Add(new Attachment(stream, attachment.Name, attachment.ContentType ?? "application/octet-stream"));
                }
            }

            await _smtpClient.SendMailAsync(mailMessage);
        }

        public async Task SendEmailAsync(List<string> recipients, string subject, string body)
        {
            foreach (string recipient in recipients)
            {
                using (MailMessage mailMessage = BuildMessage(recipient, subject, body))
                {
                    await _smtpClient.SendMailAsync(mailMessage);
                }
            }
        }

        public async Task SendEmailFromBackgroundJobAsync(string recipient, string subject, string body)
        {
            using (MailMessage mailMessage = BuildMessage(recipient, subject, body))
            {
                try
                {
                    await _smtpClient.SendMailAsync(mailMessage);
                }
                catch (Exception ex)
                {
                    // We need to log because exception will not get into api global error handler from the background job
                    _logger.LogError(
                        ex,
                        "We failed to send an email to the recipient: {recipient};",
                        recipient
                    );

                    throw;
                }
            }
        }

        /// <summary>
        /// The one place a message is constructed, so every cross-cutting concern (Reply-To resolution,
        /// app-supplied headers) applies to every send path by construction. The five public methods each
        /// built their own message and had to remember both, which is one line per concern per method to
        /// forget — and a send path added later would silently skip them.
        /// </summary>
        private MailMessage BuildMessage(string recipient, string subject, string body, EmailSender? from = null, EmailSender? replyTo = null)
        {
            MailMessage mailMessage = new(BuildFromAddress(from), new MailAddress(recipient))
            {
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and doesn't throw
                IsBodyHtml = true,
            };

            ApplyReplyTo(mailMessage, from, replyTo);

            IDictionary<string, string>? headers = _headerProvider?.HeadersFor(recipient);

            if (headers is { Count: > 0 })
                foreach (KeyValuePair<string, string> header in headers)
                    mailMessage.Headers[header.Key] = header.Value;

            return mailMessage;
        }

        private MailAddress BuildFromAddress(EmailSender? sender)
        {
            EmailSender s = sender ?? _emailSettings.EmailSender;
            return string.IsNullOrWhiteSpace(s.Name)
                ? new MailAddress(s.Email)
                : new MailAddress(s.Email, s.Name);
        }

        private void ApplyReplyTo(MailMessage mailMessage, EmailSender? from, EmailSender? replyTo = null)
        {
            EmailSender? resolved = _emailSettings.ResolveReplyTo(from, replyTo);

            if (string.IsNullOrWhiteSpace(resolved?.Email))
                return;

            // resolved != null past the guard — a null resolved would have returned above.
            mailMessage.ReplyToList.Add(string.IsNullOrWhiteSpace(resolved!.Name)
                ? new MailAddress(resolved.Email)
                : new MailAddress(resolved.Email, resolved.Name));
        }
    }
}
