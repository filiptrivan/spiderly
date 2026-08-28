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
using Spiderly.Shared.Interfaces;

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
        public async Task Per_call_sender_override_carries_an_explicit_reply_to()
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
                from: new EmailSender { Email = "noreply@brand.com", Name = "Brand" },
                replyTo: new EmailSender { Email = "office@brand.com", Name = "Brand" });

            JsonElement payload = handler.LastPayload();
            Assert.Equal("noreply@brand.com", payload.GetProperty("sender").GetProperty("email").GetString());
            Assert.Equal("office@brand.com", payload.GetProperty("replyTo").GetProperty("email").GetString());
        }

        // An app that needs to decorate every outbound message (a List-Unsubscribe pointing at its own
        // opt-out endpoint, a tracking header) registers a provider rather than threading a parameter
        // through every send site — including the ones it does not call directly, like the framework's
        // own verification email.
        [Fact]
        public async Task A_registered_header_provider_decorates_the_payload()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(
                handler,
                new EmailOptions
                {
                    EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                    BrevoApiKey = "test-key",
                },
                new StubHeaderProvider("customer@example.com", new Dictionary<string, string>
                {
                    ["List-Unsubscribe"] = "<https://example.com/opt-out>",
                }));

            await service.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

            JsonElement headers = handler.LastPayload().GetProperty("headers");
            Assert.Equal("<https://example.com/opt-out>", headers.GetProperty("List-Unsubscribe").GetString());
        }

        // The provider is optional and per-recipient: no provider, or one that declines this
        // recipient, must leave the payload exactly as it was.
        [Fact]
        public async Task No_headers_are_sent_without_a_provider()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(handler, new EmailOptions
            {
                EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                BrevoApiKey = "test-key",
            });

            await service.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

            Assert.False(handler.LastPayload().TryGetProperty("headers", out _));
        }

        [Fact]
        public async Task The_verification_email_is_decorated_too()
        {
            RecordingHandler handler = new();
            BrevoEmailingService service = NewService(
                handler,
                new EmailOptions
                {
                    EmailSender = new EmailSender { Email = "no-reply@example.com", Name = "Example Shop" },
                    BrevoApiKey = "test-key",
                },
                new StubHeaderProvider("customer@example.com", new Dictionary<string, string>
                {
                    ["List-Unsubscribe"] = "<https://example.com/opt-out>",
                }));

            await service.SendVerificationEmailAsync(
                "customer@example.com",
                new DTO.EmailVerifyUIDTO { Subject = "Code", Body = "<p>123456</p>" });

            JsonElement headers = handler.LastPayload().GetProperty("headers");
            Assert.Equal("<https://example.com/opt-out>", headers.GetProperty("List-Unsubscribe").GetString());
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

        private static BrevoEmailingService NewService(
            RecordingHandler handler,
            EmailOptions options,
            IOutboundEmailHeaderProvider? headerProvider = null) =>
            new(new SingleClientFactory(handler), NullLogger<BrevoEmailingService>.Instance, Options.Create(options), headerProvider);

        private sealed class StubHeaderProvider : IOutboundEmailHeaderProvider
        {
            private readonly string _recipient;
            private readonly IDictionary<string, string> _headers;

            public StubHeaderProvider(string recipient, IDictionary<string, string> headers)
            {
                _recipient = recipient;
                _headers = headers;
            }

            public IDictionary<string, string>? HeadersFor(string recipient) =>
                recipient == _recipient ? _headers : null;
        }

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
