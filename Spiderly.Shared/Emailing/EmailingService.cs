using Microsoft.Extensions.Logging;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using System.Net.Mail;
using System.Text;

namespace Spiderly.Shared.Emailing
{
    public class EmailingService : IEmailingService
    {
        private readonly SmtpClient _smtpClient;
        private readonly ILogger<EmailingService> _logger;

        public EmailingService(ILogger<EmailingService> logger)
        {
            _smtpClient = Helper.GetSmtpClient();
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template)
        {
            using (MailMessage mailMessage = new MailMessage(BuildFromAddress(null), new MailAddress(toEmail))
            {
                Subject = template.Subject,
                Body = template.Body,
                BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and don't throw the exception
                IsBodyHtml = true
            })
            {
                await _smtpClient.SendMailAsync(mailMessage); // https://stackoverflow.com/questions/11120350/how-to-check-programmatically-if-an-email-is-existing-or-not
            }
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, EmailSender from = null)
        {
            using (MailMessage mailMessage = new MailMessage(BuildFromAddress(from), new MailAddress(recipient))
            {
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and don't throw the exception
                IsBodyHtml = true,
            })
            {
                await _smtpClient.SendMailAsync(mailMessage);
            }
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, EmailSender from = null)
        {
            using MailMessage mailMessage = new(BuildFromAddress(from), new MailAddress(recipient))
            {
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
            };

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
                using (MailMessage mailMessage = new MailMessage(BuildFromAddress(null), new MailAddress(recipient))
                {
                    Subject = subject,
                    Body = body,
                    BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and don't throw the exception
                    IsBodyHtml = true,
                })
                {
                    await _smtpClient.SendMailAsync(mailMessage);
                }
            }
        }

        public async Task SendEmailFromBackgroundJobAsync(string recipient, string subject, string body)
        {
            using (MailMessage mailMessage = new MailMessage(BuildFromAddress(null), new MailAddress(recipient))
            {
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8, // Without this, the email is not sent, and don't throw the exception
                IsBodyHtml = true,
            })
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

        private static MailAddress BuildFromAddress(EmailSender sender)
        {
            EmailSender s = sender ?? SettingsProvider.Current.EmailSender;
            return string.IsNullOrWhiteSpace(s.Name)
                ? new MailAddress(s.Email)
                : new MailAddress(s.Email, s.Name);
        }
    }
}
