using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Spiderly.Shared;
using Spiderly.Shared.Emailing;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Guards the config↔options contract. appsettings is bound reflectively, so a mismatch between the
    /// documented config shape and the options class binds <em>silently</em> and only fails at runtime — exactly
    /// how a string-typed <c>EmailSender</c> (vs the <c>{ Email, Name }</c> object the code expects) shipped
    /// undetected and surfaced as Brevo "sender is missing". These tests pin the shapes so such drift fails CI,
    /// and the matching <c>ValidateOnStart</c> guards (StartupExtensions) turn a missed value into a boot failure.
    /// </summary>
    public class OptionsBindingTests
    {
        private static IConfigurationSection Section(Dictionary<string, string> values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build()
                .GetSection(Settings.ConfigurationSection);

        [Fact]
        public void EmailSender_binds_from_an_object()
        {
            EmailOptions options = Section(new()
            {
                ["AppSettings:Spiderly.Shared:EmailSender:Email"] = "noreply@example.com",
                ["AppSettings:Spiderly.Shared:EmailSender:Name"] = "Example",
            }).Get<EmailOptions>();

            Assert.NotNull(options.EmailSender);
            Assert.Equal("noreply@example.com", options.EmailSender.Email);
            Assert.Equal("Example", options.EmailSender.Name);
        }

        [Fact]
        public void EmailSender_as_a_string_yields_no_usable_sender()
        {
            // Regression for the silent string→object drift: a scalar EmailSender binds to an empty object
            // (Email == null), which later fails the send with Brevo "sender is missing". The StartupExtensions
            // ValidateOnStart guard turns this into a boot-time failure; this documents the binding behaviour.
            EmailOptions options = Section(new()
            {
                ["AppSettings:Spiderly.Shared:EmailSender"] = "noreply@example.com",
            }).Get<EmailOptions>();

            Assert.True(string.IsNullOrWhiteSpace(options.EmailSender?.Email));
        }

        [Fact]
        public void Core_options_bind_from_a_representative_appsettings()
        {
            // Binds the documented Spiderly.Shared config shape to the options classes; if a future refactor
            // changes an option's shape (as the EmailSender refactor did), this fails — forcing config/schema/docs
            // to be updated alongside the code.
            IConfigurationSection section = Section(new()
            {
                ["AppSettings:Spiderly.Shared:JwtKey"] = "test-jwt-signing-key",
                ["AppSettings:Spiderly.Shared:EmailSender:Email"] = "noreply@example.com",
                ["AppSettings:Spiderly.Shared:BrevoApiKey"] = "test-brevo-key",
                ["AppSettings:Spiderly.Shared:AccessTokenKey"] = "access_token",
                // AdminRecipients (renamed from UnhandledExceptionRecipients) — a stale key in consumer config
                // binds to null and silently drops all admin/ops notifications.
                ["AppSettings:Spiderly.Shared:AdminRecipients:0"] = "ops@example.com",
            });

            Assert.Equal("test-jwt-signing-key", section.Get<JwtOptions>().JwtKey);
            Assert.Equal("noreply@example.com", section.Get<EmailOptions>().EmailSender.Email);
            Assert.Equal("test-brevo-key", section.Get<EmailOptions>().BrevoApiKey);
            Assert.Equal("access_token", section.Get<TokenKeyOptions>().AccessTokenKey);
            Assert.Equal("ops@example.com", Assert.Single(section.Get<NotificationOptions>().AdminRecipients));
        }

        [Fact]
        public void Outbox_retry_options_bind_from_default_and_per_handler()
        {
            OutboxOptions options = Section(new()
            {
                ["AppSettings:Spiderly.Shared:Outbox:Default:MaxAttempts"] = "15",
                ["AppSettings:Spiderly.Shared:Outbox:Default:MaxBackoffMinutes"] = "90",
                ["AppSettings:Spiderly.Shared:Outbox:Handlers:WingsExport:MaxAttempts"] = "20",
            }).GetSection("Outbox").Get<OutboxOptions>();

            Assert.Equal(15, options.Default.MaxAttempts);
            Assert.Equal(90, options.Default.MaxBackoffMinutes);
            Assert.Equal(20, options.Handlers["WingsExport"].MaxAttempts);
            // Unset field stays null so it falls through to the next layer at resolution time.
            Assert.Null(options.Handlers["WingsExport"].MaxBackoffMinutes);
        }
    }
}
