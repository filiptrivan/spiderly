using Spiderly.Shared.Emailing;

namespace Spiderly.Shared
{
    /// <summary>
    /// Email/SMTP options. Bound from the <c>AppSettings:Spiderly.Shared</c> configuration section and
    /// injected into the emailing services as <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public class EmailOptions
    {
        /// <summary>
        /// Default "From" address for transactional emails. <c>Email</c> is also used as the SMTP
        /// username when the <see cref="EmailingService"/> (SMTP) implementation is active.
        /// </summary>
        public EmailSender EmailSender { get; set; } = new();

        /// <summary>SMTP password for the <see cref="EmailSender"/> account.</summary>
        public string EmailSenderPassword { get; set; }

        /// <summary>SMTP host name.</summary>
        public string SmtpHost { get; set; } = "smtp.gmail.com";

        /// <summary>SMTP port.</summary>
        public int SmtpPort { get; set; } = 587;

        /// <summary>Brevo API key, used by the Brevo emailing implementation.</summary>
        public string BrevoApiKey { get; set; }
    }
}
