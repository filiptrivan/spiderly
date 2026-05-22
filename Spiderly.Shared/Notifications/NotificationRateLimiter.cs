using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Debounces duplicate operational notifications using an in-memory sliding window keyed by an
    /// event key. Registered as a singleton so the rate-limit cache is shared across all callers.
    /// Replaces the former static <c>Helper.ShouldSendNotification</c>.
    /// </summary>
    public class NotificationRateLimiter
    {
        private readonly NotificationOptions _settings;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _rateLimitCache = new();
        private DateTimeOffset _lastCacheCleanup = DateTimeOffset.UtcNow;

        public NotificationRateLimiter(IOptions<NotificationOptions> options)
        {
            _settings = options.Value;
        }

        /// <summary>
        /// Returns <c>true</c> the first time an <paramref name="eventKey"/> is seen and again only after
        /// <see cref="NotificationOptions.NotificationRateLimitMinutes"/> have elapsed since the last send.
        /// A non-positive rate limit disables debouncing (always <c>true</c>).
        /// </summary>
        public bool ShouldSend(string eventKey)
        {
            if (_settings.NotificationRateLimitMinutes <= 0)
                return true;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            int rateLimitMinutes = _settings.NotificationRateLimitMinutes;
            DateTimeOffset threshold = now.AddMinutes(-rateLimitMinutes);

            // Lazy cleanup: prune stale entries periodically
            DateTimeOffset cleanupThreshold = now.AddMinutes(-rateLimitMinutes * 2);
            if (_lastCacheCleanup < cleanupThreshold)
            {
                _lastCacheCleanup = now;
                foreach (var kvp in _rateLimitCache)
                {
                    if (kvp.Value < cleanupThreshold)
                        _rateLimitCache.TryRemove(kvp.Key, out _);
                }
            }

            bool shouldSend = false;
            _rateLimitCache.AddOrUpdate(
                eventKey,
                addValueFactory: _ => { shouldSend = true; return now; },
                updateValueFactory: (_, existing) =>
                {
                    if (existing < threshold) { shouldSend = true; return now; }
                    return existing;
                });
            return shouldSend;
        }

        /// <summary>
        /// Debounces by exception type + message, so a recurring exception alerts at most once per window.
        /// </summary>
        public bool ShouldSend(Exception ex)
        {
            string key = $"{ex.GetType().FullName}:{ex.Message.GetHashCode()}";
            return ShouldSend(key);
        }
    }
}
