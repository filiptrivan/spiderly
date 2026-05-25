using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Debounces duplicate operational notifications using an in-memory sliding window keyed by an event key.
    /// Registered as a singleton so the cache is shared across callers. The window is supplied per call (a
    /// notification's <c>DedupeWindow</c>) and falls back to the global <see cref="NotificationOptions.NotificationRateLimitMinutes"/>.
    /// </summary>
    public class NotificationRateLimiter
    {
        // How often the lazy cleanup may scan. Independent of the dedupe window — correctness comes from the
        // per-entry window check in ShouldSend; this only bounds memory-hygiene scans.
        private const int CleanupIntervalMinutes = 10;

        private readonly NotificationOptions _settings;
        private readonly ConcurrentDictionary<string, Entry> _rateLimitCache = new();
        private long _lastCleanupTicks = DateTimeOffset.UtcNow.UtcTicks;

        public NotificationRateLimiter(IOptions<NotificationOptions> options)
        {
            _settings = options.Value;
        }

        /// <summary>
        /// Returns <c>true</c> the first time an <paramref name="eventKey"/> is seen and again only after the
        /// key's window has elapsed since the last send. <paramref name="window"/> defaults to the global
        /// <see cref="NotificationOptions.NotificationRateLimitMinutes"/> and sets the window for the key's
        /// bucket; a non-positive window disables debouncing (always <c>true</c>).
        /// </summary>
        public bool ShouldSend(string eventKey, TimeSpan? window = null)
        {
            TimeSpan effectiveWindow = window ?? TimeSpan.FromMinutes(_settings.NotificationRateLimitMinutes);
            if (effectiveWindow <= TimeSpan.Zero)
                return true;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            PruneStaleEntries(now);

            bool shouldSend = false;
            _rateLimitCache.AddOrUpdate(
                eventKey,
                addValueFactory: _ =>
                {
                    shouldSend = true;
                    return new Entry(now, effectiveWindow);
                },
                updateValueFactory: (_, existing) =>
                {
                    // Gate on the window stored when the bucket was created, so a key's suppression window is
                    // stable for its lifetime regardless of which caller hits it next; a changed window only
                    // takes effect on the next send (when the bucket is recreated).
                    if (existing.Time < now - existing.Window)
                    {
                        shouldSend = true;
                        return new Entry(now, effectiveWindow);
                    }
                    return existing;
                });
            return shouldSend;
        }

        // Lazy, best-effort cleanup: a single thread (the CAS winner) per interval drops entries past 2× their own
        // window. Memory hygiene only — the per-entry check in ShouldSend is what enforces correctness.
        private void PruneStaleEntries(DateTimeOffset now)
        {
            long last = Interlocked.Read(ref _lastCleanupTicks);
            if (now.UtcTicks - last < TimeSpan.FromMinutes(CleanupIntervalMinutes).Ticks)
                return;

            // Only the thread that advances the timestamp scans; the rest skip this round.
            if (Interlocked.CompareExchange(ref _lastCleanupTicks, now.UtcTicks, last) != last)
                return;

            foreach (KeyValuePair<string, Entry> kvp in _rateLimitCache)
            {
                if (kvp.Value.Time < now - TimeSpan.FromTicks(kvp.Value.Window.Ticks * 2))
                    _rateLimitCache.TryRemove(kvp.Key, out _);
            }
        }

        private readonly record struct Entry(DateTimeOffset Time, TimeSpan Window);
    }
}
