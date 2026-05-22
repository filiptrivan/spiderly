using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Sends operational alerts to a Telegram chat. Registered as a singleton (owns its
    /// <see cref="HttpClient"/>). Replaces the former static <c>Helper</c> Telegram methods and
    /// <c>Helper.IsTelegramConfigured</c>.
    /// </summary>
    public class TelegramNotifier
    {
        private readonly NotificationOptions _settings;
        private readonly ILogger<TelegramNotifier> _logger;
        private readonly HttpClient _httpClient = new();

        private const int TelegramMaxLength = 4096;
        private const string TruncationMarker = "\n...[truncated]...\n";

        public TelegramNotifier(IOptions<NotificationOptions> options, ILogger<TelegramNotifier> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        /// <summary>True when both a bot token and chat id are configured.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settings.TelegramBotToken) &&
            !string.IsNullOrWhiteSpace(_settings.TelegramChatId);

        /// <summary>Sends <paramref name="text"/> to the configured Telegram chat; logs and swallows failures.</summary>
        public async Task SendAsync(string text)
        {
            try
            {
                string truncated = TruncateForTelegram(text);

                string url = $"https://api.telegram.org/bot{_settings.TelegramBotToken}/sendMessage";
                using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, new { chat_id = _settings.TelegramChatId, text = truncated });
                if (!response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Telegram notification failed: {StatusCode} — {Body}", response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram notification not sent");
            }
        }

        /// <summary>Sends a formatted unhandled-exception alert (prefixed with the application name).</summary>
        public async Task SendUnhandledExceptionAsync(long? userId, string exceptionString)
        {
            string text = $$"""
[{{_settings.ApplicationName}}] Unhandled Exception
User ID: {{userId}}
{{exceptionString}}
""";
            await SendAsync(text);
        }

        private static string TruncateForTelegram(string text)
        {
            if (text.Length <= TelegramMaxLength)
                return text;

            const int headBudget = 500;
            int tailBudget = TelegramMaxLength - headBudget - TruncationMarker.Length;
            return text[..headBudget] + TruncationMarker + text[^tailBudget..];
        }
    }
}
