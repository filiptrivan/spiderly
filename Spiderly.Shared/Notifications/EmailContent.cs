namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Rendered email content produced by a notification's <see cref="IEmailNotification.ToEmail"/>.
    /// </summary>
    public class EmailContent
    {
        /// <summary>Creates email content.</summary>
        /// <param name="subject">The email subject.</param>
        /// <param name="body">The email body (HTML supported).</param>
        public EmailContent(string subject, string body)
        {
            Subject = subject;
            Body = body;
        }

        /// <summary>The email subject line.</summary>
        public string Subject { get; }

        /// <summary>The email body (HTML supported).</summary>
        public string Body { get; }
    }
}
