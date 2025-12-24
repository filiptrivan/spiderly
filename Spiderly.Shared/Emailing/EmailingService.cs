using Serilog;
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

        public EmailingService()
        {
            _smtpClient = Helper.GetSmtpClient();
        }

        public async Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template)
        {
            using (MailMessage mailMessage = new MailMessage(SettingsProvider.Current.EmailSender, toEmail)
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

        public async Task SendEmailAsync(string recipient, string subject, string body, string from = null)
        {
            using (MailMessage mailMessage = new MailMessage(from ?? SettingsProvider.Current.EmailSender, recipient)
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

        public async Task SendEmailAsync(List<string> recipients, string subject, string body)
        {
            foreach (string recipient in recipients)
            {
                using (MailMessage mailMessage = new MailMessage(SettingsProvider.Current.EmailSender, recipient)
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
            using (MailMessage mailMessage = new MailMessage(SettingsProvider.Current.EmailSender, recipient)
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
                    Log.Error(
                        ex,
                        "We failed to send an email to the recipient: {recipient};",
                        recipient
                    );

                    throw;
                }
            }
        }

    }
}
