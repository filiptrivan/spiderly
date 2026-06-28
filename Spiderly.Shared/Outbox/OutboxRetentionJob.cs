using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// Recurring purge of fully-handled outbox rows (<c>DispatchedAt</c> set — delivered or admin-dismissed) older than
    /// <see cref="OutboxOptions.RetentionDays"/>, so the table doesn't grow unbounded. PENDING and DEAD-LETTERED rows
    /// (<c>DispatchedAt IS NULL</c>) are never touched — dead-letters await human triage (Retry/Dismiss). Generic over
    /// the consumer's outbox entity <typeparamref name="TOutbox"/>; scheduled by
    /// <c>SpiderlyUseOutboxRecurringJob&lt;TOutbox&gt;()</c>. Storage/backup hygiene only — the partial pending index
    /// keeps the sweep fast regardless of how many handled rows accumulate.
    ///
    /// <para><c>[AutomaticRetry(Attempts = 0)]</c> — the daily schedule is the retry; a transient failure just waits for
    /// the next run rather than piling up Hangfire retries.</para>
    /// </summary>
    /// <typeparam name="TOutbox">The consumer's <c>[SpiderlyEntity]</c> implementing <see cref="IOutboxMessage"/>.</typeparam>
    [AutomaticRetry(Attempts = 0)]
    public class OutboxRetentionJob<TOutbox>
        where TOutbox : class, IOutboxMessage, new()
    {
        private readonly IApplicationDbContext _context;
        private readonly OutboxOptions _options;
        private readonly ILogger<OutboxRetentionJob<TOutbox>> _logger;

        /// <summary>Creates the retention job over the job-scoped context, the bound options, and a logger.</summary>
        public OutboxRetentionJob(
            IApplicationDbContext context,
            IOptions<OutboxOptions> options,
            ILogger<OutboxRetentionJob<TOutbox>> logger)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>Bulk-deletes handled rows older than the retention window. Leaf rows, so no cascade.</summary>
        public async Task PurgeAsync()
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

            int deleted = await _context.DbSet<TOutbox>()
                .Where(x => x.DispatchedAt != null && x.DispatchedAt < cutoff)
                .ExecuteDeleteAsync();

            if (deleted > 0)
                _logger.LogInformation(
                    "Outbox retention: purged {Count} dispatched row(s) older than {Days} days.", deleted, _options.RetentionDays);
        }
    }
}
