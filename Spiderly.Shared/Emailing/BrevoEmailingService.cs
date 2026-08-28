using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Interfaces;
using System.Net.Http.Json;

namespace Spiderly.Shared.Emailing
{
    /// <summary>
    /// Sends transactional emails via the Brevo (formerly Sendinblue) REST API.
    /// Requires a named <c>"Brevo"</c> HttpClient registered through
    /// <see cref="Extensions.StartupExtensions.SpiderlyAddBrevoHttpClient"/>.
    /// </summary>
    public class BrevoEmailingService : IEmailingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BrevoEmailingService> _logger;
        private readonly EmailOptions _emailSettings;

        public BrevoEmailingService(IHttpClientFactory httpClientFactory, ILogger<BrevoEmailingService> logger, IOptions<EmailOptions> emailOptions)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _emailSettings = emailOptions.Value;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_emailSettings.BrevoApiKey);
        }

        public async Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template)
        {
            await SendViaBrevoAsync(toEmail, template.Subject, template.Body);
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, EmailSender? from = null, EmailSender? replyTo = null)
        {
            await SendViaBrevoAsync(recipient, subject, body, from, replyTo);
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, EmailSender? from = null, EmailSender? replyTo = null)
        {
            await SendViaBrevoAsync(recipient, subject, body, from, replyTo, attachments);
        }

        public async Task SendEmailAsync(List<string> recipients, string subject, string body)
        {
            foreach (string recipient in recipients)
            {
                await SendViaBrevoAsync(recipient, subject, body);
            }
        }

        public async Task SendEmailFromBackgroundJobAsync(string recipient, string subject, string body)
        {
            try
            {
                await SendViaBrevoAsync(recipient, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "We failed to send an email to the recipient: {recipient};",
                    recipient
                );

                throw;
            }
        }

        private async Task SendViaBrevoAsync(string recipient, string subject, string body, EmailSender? from = null, EmailSender? replyTo = null, IEnumerable<EmailAttachment>? attachments = null)
        {
            EmailSender sender = from ?? _emailSettings.EmailSender;

            Dictionary<string, object> payload = new()
            {
                ["to"] = new[] { new { email = recipient } },
                ["subject"] = subject,
                ["htmlContent"] = body,
            };

            payload["sender"] = BuildAddressPayload(sender);

            EmailSender? resolvedReplyTo = _emailSettings.ResolveReplyTo(from, replyTo);

            if (!string.IsNullOrWhiteSpace(resolvedReplyTo?.Email))
                payload["replyTo"] = BuildAddressPayload(resolvedReplyTo!); // resolvedReplyTo != null — the guard just checked resolvedReplyTo?.Email

            // Brevo expects attachments as [{ name, content }] with base64 content.
            object[]? brevoAttachments = attachments?
                .Where(a => a != null && !string.IsNullOrEmpty(a.ContentBase64))
                .Select(a => (object)new { name = a.Name, content = a.ContentBase64 })
                .ToArray();

            if (brevoAttachments is { Length: > 0 })
                payload["attachment"] = brevoAttachments;

            HttpClient httpClient = _httpClientFactory.CreateClient("Brevo");

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                "smtp/email",
                payload
            );

            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Brevo API error {StatusCode} for recipient {Recipient}: {ResponseBody}",
                    response.StatusCode,
                    recipient,
                    responseBody
                );
                throw new HttpRequestException($"Brevo API returned {response.StatusCode}: {responseBody}");
            }
        }

        // Omit `name` when blank — some clients render an empty display name as literally "".
        private static object BuildAddressPayload(EmailSender address)
        {
            return string.IsNullOrWhiteSpace(address.Name)
                ? new { email = address.Email }
                : new { email = address.Email, name = address.Name };
        }
    }
}
