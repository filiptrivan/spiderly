using Microsoft.Extensions.Logging;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Interfaces;
using System.Net.Http.Json;

namespace Spiderly.Shared.Emailing
{
    /// <summary>
    /// Sends transactional emails via the Brevo (formerly Sendinblue) REST API.
    /// Requires a named <c>"Brevo"</c> HttpClient registered through
    /// <see cref="Extensions.StartupExtensions.SpiderlyAddBrevoEmailingService"/>.
    /// </summary>
    public class BrevoEmailingService : IEmailingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BrevoEmailingService> _logger;

        public BrevoEmailingService(IHttpClientFactory httpClientFactory, ILogger<BrevoEmailingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template)
        {
            await SendViaBrevoAsync(toEmail, template.Subject, template.Body);
        }

        public async Task SendEmailAsync(string recipient, string subject, string body, string from = null)
        {
            await SendViaBrevoAsync(recipient, subject, body, from);
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

        private async Task SendViaBrevoAsync(string recipient, string subject, string body, string from = null)
        {
            string senderEmail = from ?? SettingsProvider.Current.EmailSender;

            var payload = new
            {
                sender = new { email = senderEmail },
                to = new[] { new { email = recipient } },
                subject,
                htmlContent = body
            };

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
    }
}
