using Spiderly.Shared.DTO;
using Spiderly.Shared.Emailing;

namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Provides email sending functionality for the application.
    /// Users can implement this interface to provide their own email service implementation.
    /// </summary>
    public interface IEmailingService
    {
        /// <summary>
        /// Sends a verification email using a pre-defined template
        /// </summary>
        /// <param name="toEmail">The recipient email address</param>
        /// <param name="template">The email template containing subject and body</param>
        Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template);

        /// <summary>
        /// Sends an email to a single recipient
        /// </summary>
        /// <param name="recipient">The recipient email address</param>
        /// <param name="subject">The email subject</param>
        /// <param name="body">The email body (HTML supported)</param>
        /// <param name="from">Optional sender. If not provided, uses the default sender from settings. Pass <see cref="EmailSender.Name"/> to control the display name shown in the recipient's inbox.</param>
        /// <param name="replyTo">Optional Reply-To. Without it a per-call <paramref name="from"/> override sends with no Reply-To at all (the configured default Reply-To never rides along), so replies to a no-reply override dead-end — pass this to route them to a monitored inbox.</param>
        Task SendEmailAsync(string recipient, string subject, string body, EmailSender? from = null, EmailSender? replyTo = null);

        /// <summary>
        /// Sends an email with binary attachments (e.g., PDF documents).
        /// </summary>
        Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, EmailSender? from = null, EmailSender? replyTo = null);

        /// <summary>
        /// Sends the same email to multiple recipients
        /// </summary>
        /// <param name="recipients">List of recipient email addresses</param>
        /// <param name="subject">The email subject</param>
        /// <param name="body">The email body (HTML supported)</param>
        Task SendEmailAsync(List<string> recipients, string subject, string body);

        /// <summary>
        /// Sends an email from a background job with enhanced error logging
        /// </summary>
        /// <param name="recipient">The recipient email address</param>
        /// <param name="subject">The email subject</param>
        /// <param name="body">The email body (HTML supported)</param>
        Task SendEmailFromBackgroundJobAsync(string recipient, string subject, string body);

        /// <summary>
        /// Whether this implementation has enough configuration to actually deliver email.
        /// Used (e.g.) to decide whether to surface a login verification code in dev when delivery
        /// is unavailable. Each implementation checks what it needs (SMTP credentials, an API key, etc.).
        /// </summary>
        bool IsConfigured();
    }
}
