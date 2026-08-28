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

        /// <summary>
        /// Optional "Reply-To" address attached to emails sent with the default <see cref="EmailSender"/>.
        /// Useful when the sender is a no-reply address: replies land in a monitored inbox instead of bouncing.
        /// Not applied when a caller overrides the sender per call — the override owns the whole identity.
        /// Unset (or empty <c>Email</c>) means no Reply-To header.
        /// </summary>
        public EmailSender? EmailReplyTo { get; set; }

        /// <summary>
        /// Resolves the Reply-To for a send: an explicit per-call <paramref name="replyTo"/> always wins;
        /// otherwise the configured <see cref="EmailReplyTo"/> rides with the default sender identity only —
        /// a per-call <paramref name="from"/> override owns the whole identity and never inherits it (pass
        /// <paramref name="replyTo"/> alongside the override to route its replies to a monitored inbox).
        /// Every <see cref="Interfaces.IEmailingService"/> implementation must route through this so the
        /// policy can't drift between providers.
        /// </summary>
        public EmailSender? ResolveReplyTo(EmailSender? from, EmailSender? replyTo = null)
        {
            return replyTo ?? (from == null ? EmailReplyTo : null);
        }

        /// <summary>SMTP password for the <see cref="EmailSender"/> account.</summary>
        public string? EmailSenderPassword { get; set; }

        /// <summary>SMTP host name.</summary>
        public string SmtpHost { get; set; } = "smtp.gmail.com";

        /// <summary>SMTP port.</summary>
        public int SmtpPort { get; set; } = 587;

        /// <summary>Brevo API key, used by the Brevo emailing implementation.</summary>
        public string? BrevoApiKey { get; set; }
    }
}
