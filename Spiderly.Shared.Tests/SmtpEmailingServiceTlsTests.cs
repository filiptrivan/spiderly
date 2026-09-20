using System.Net.Mail;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Emailing;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins the SMTP sender's TLS setting: TLS by default, and off only when a consumer asks for it.
    ///
    /// <para>The value is set on the <see cref="SmtpClient"/> in the constructor and never read again, so
    /// nothing else in the codebase observes it — which is exactly why it can be "tidied" back to a
    /// hardcoded <c>true</c> without a single other test turning red. The default matters most: a
    /// regression the other way would send real credentials in the clear.</para>
    /// </summary>
    public class SmtpEmailingServiceTlsTests
    {
        [Fact]
        public void Tls_is_on_unless_a_consumer_turns_it_off()
        {
            Assert.True(EnableSslOf(new EmailOptions { SmtpHost = "smtp.example.com" }));
        }

        [Fact]
        public void Tls_is_off_when_the_consumer_turns_it_off()
        {
            // The case this setting exists for: a mail catcher (Mailpit, MailHog) on a private Docker
            // network, whose self-signed certificate no client trusts.
            Assert.False(EnableSslOf(new EmailOptions { SmtpHost = "mailpit", SmtpEnableSsl = false }));
        }

        private static bool EnableSslOf(EmailOptions options)
        {
            EmailingService service = new(
                NullLogger<EmailingService>.Instance,
                Options.Create(options));

            FieldInfo field = typeof(EmailingService)
                .GetField("_smtpClient", BindingFlags.Instance | BindingFlags.NonPublic)!;

            return ((SmtpClient)field.GetValue(service)!).EnableSsl;
        }
    }
}
