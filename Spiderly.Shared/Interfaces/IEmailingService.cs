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
        /// <param name="from">Optional sender email address. If not provided, uses the default sender from settings</param>
        Task SendEmailAsync(string recipient, string subject, string body, string from = null);

        /// <summary>
        /// Sends an email with binary attachments (e.g., PDF documents).
        /// </summary>
        Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, string from = null);

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
    }
}
