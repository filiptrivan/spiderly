using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared;
using Spiderly.Shared.Emailing;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins the Brevo request payload — the wire contract Brevo actually receives. The Reply-To rules
    /// (attached only for the default sender identity, omitted entirely when unconfigured) are invisible
    /// in a 201 response, so a payload regression would ship silently without these.
    /// </summary>
    public class BrevoEmailingServiceTests
    {
        [Fact]
        public async Task Configured_reply_to_is_attached_for_the_default_sender()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(handler, new EmailOptions
            {
                EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                EmailReplyTo = new EmailSender { Email = "info@example.com", Name = "Example Shop" },
                BrevoApiKey = "test-key",
            });

            await service.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

            JsonElement payload = handler.LastPayload();
            Assert.Equal("info@example.com", payload.GetProperty("replyTo").GetProperty("email").GetString());
            Assert.Equal("Example Shop", payload.GetProperty("replyTo").GetProperty("name").GetString());
        }

        [Fact]
        public async Task Unconfigured_reply_to_is_omitted_from_the_payload()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(handler, new EmailOptions
            {
                EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                BrevoApiKey = "test-key",
            });

            await service.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

            Assert.False(handler.LastPayload().TryGetProperty("replyTo", out _));
        }

        [Fact]
        public async Task Per_call_sender_override_does_not_carry_the_configured_reply_to()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(handler, new EmailOptions
            {
                EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                EmailReplyTo = new EmailSender { Email = "info@example.com", Name = "Example Shop" },
                BrevoApiKey = "test-key",
            });

            await service.SendEmailAsync(
                "customer@example.com",
                "Subject",
                "<p>Body</p>",
                from: new EmailSender { Email = "noreply@brand.com", Name = "Brand" });

            JsonElement payload = handler.LastPayload();
            Assert.Equal("noreply@brand.com", payload.GetProperty("sender").GetProperty("email").GetString());
            Assert.False(payload.TryGetProperty("replyTo", out _));
        }

        [Fact]
        public async Task Blank_reply_to_name_is_omitted_from_the_reply_to_object()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(handler, new EmailOptions
            {
                EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                EmailReplyTo = new EmailSender { Email = "info@example.com" },
                BrevoApiKey = "test-key",
            });

            await service.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

            JsonElement replyTo = handler.LastPayload().GetProperty("replyTo");
            Assert.Equal("info@example.com", replyTo.GetProperty("email").GetString());
            Assert.False(replyTo.TryGetProperty("name", out _));
        }

        private static BrevoEmailingService NewService(RecordingHandler handler, EmailOptions options) =>
            new(new SingleClientFactory(handler), NullLogger<BrevoEmailingService>.Instance, Options.Create(options));

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private string _lastBody = null!;

            public JsonElement LastPayload() => JsonDocument.Parse(_lastBody).RootElement;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _lastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"messageId":"test"}"""),
                };
            }
        }

        private sealed class SingleClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public SingleClientFactory(HttpMessageHandler handler)
            {
                _handler = handler;
            }

            public HttpClient CreateClient(string name) =>
                new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.brevo.com/v3/") };
        }
    }
}
