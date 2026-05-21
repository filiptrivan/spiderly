using Spiderly.Shared.Emailing;

namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the email/SMTP settings. Implemented by <see cref="Settings"/> and injected
    /// into the emailing services, so they depend on configuration passed in rather than the global
    /// mutable <c>SettingsProvider</c> static.
    /// </summary>
    public interface IEmailSettings
    {
        /// <summary>Default "From" address (and SMTP username for the SMTP implementation).</summary>
        EmailSender EmailSender { get; }

        /// <summary>SMTP password for the <see cref="EmailSender"/> account.</summary>
        string EmailSenderPassword { get; }

        /// <summary>SMTP host name.</summary>
        string SmtpHost { get; }

        /// <summary>SMTP port.</summary>
        int SmtpPort { get; }

        /// <summary>Brevo API key, used by the Brevo emailing implementation.</summary>
        string BrevoApiKey { get; }
    }
}
