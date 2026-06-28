using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Outbox
{
    /// <summary>A point-in-time snapshot of outbox backlog health.</summary>
    /// <param name="OldestDueMinutes">Age (minutes) of the oldest still-pending row that is due now (excludes backing-off and dead-lettered rows). 0 when none.</param>
    /// <param name="DeadLetters">Count of rows that hit the retry cap and are parked at the <see cref="OutboxRetryPolicy.NeverRetry"/> sentinel, awaiting human triage.</param>
    /// <param name="Alert">True when the oldest-due age exceeds the configured threshold, or there is at least one dead-letter.</param>
    public readonly record struct OutboxHealth(double OldestDueMinutes, int DeadLetters, bool Alert);

    /// <summary>
    /// Recurring outbox-backlog health check. The sweep itself only surfaces trouble when it hangs past the dispatcher's
    /// lock timeout; this gives proactive visibility: it measures the oldest still-due pending row's age and the
    /// dead-letter count, and on breach logs at <c>Error</c> — which the consumer's logging pipeline (e.g. a Sentry sink)
    /// turns into an alert. Generic over <typeparamref name="TOutbox"/>; scheduled by
    /// <c>SpiderlyUseOutboxRecurringJob&lt;TOutbox&gt;()</c>. Thresholds live in <see cref="OutboxOptions"/>.
    ///
    /// <para><c>[AutomaticRetry(Attempts = 0)]</c> — read-only check; the recurring schedule is the retry.</para>
    /// </summary>
    /// <typeparam name="TOutbox">The consumer's <c>[SpiderlyEntity]</c> implementing <see cref="IOutboxMessage"/>.</typeparam>
    [AutomaticRetry(Attempts = 0)]
    public class OutboxHealthJob<TOutbox>
        where TOutbox : class, IOutboxMessage, new()
    {
        private readonly IApplicationDbContext _context;
        private readonly OutboxOptions _options;
        private readonly ILogger<OutboxHealthJob<TOutbox>> _logger;

        /// <summary>Creates the health job over the job-scoped context, the bound options, and a logger.</summary>
        public OutboxHealthJob(
            IApplicationDbContext context,
            IOptions<OutboxOptions> options,
            ILogger<OutboxHealthJob<TOutbox>> logger)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>Computes health and logs an error (→ alerting) on breach.</summary>
        public async Task CheckAsync()
        {
            OutboxHealth health = await ComputeHealthAsync();

            if (health.Alert)
                _logger.LogError(
                    "Outbox health: oldest due row {AgeMinutes:F0}min old (alert >{ThresholdMinutes}min), {DeadLetters} dead-lettered row(s) awaiting triage.",
                    health.OldestDueMinutes, _options.BacklogAgeAlertMinutes, health.DeadLetters);
            else
                _logger.LogDebug(
                    "Outbox health: oldest due row {AgeMinutes:F0}min, {DeadLetters} dead-letter(s).",
                    health.OldestDueMinutes, health.DeadLetters);
        }

        /// <summary>Read-only health snapshot (also callable from a health endpoint), without logging.</summary>
        public async Task<OutboxHealth> ComputeHealthAsync()
        {
            DateTime now = DateTime.UtcNow;

            // Oldest row that is DUE now and still pending — excludes backing-off and dead-lettered rows (both carry a
            // future NextAttemptAt). A growing value = the sweep isn't keeping up (or is wedged).
            DateTime? oldestDue = await _context.DbSet<TOutbox>()
                .Where(x => x.DispatchedAt == null && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
                .OrderBy(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync();

            // Dead-lettered: crossed the retry cap, parked at the NeverRetry sentinel, awaiting human Retry/Dismiss.
            DateTime sentinel = OutboxRetryPolicy.NeverRetry;
            int deadLetters = await _context.DbSet<TOutbox>()
                .CountAsync(x => x.DispatchedAt == null && x.NextAttemptAt == sentinel);

            double oldestMinutes = oldestDue is DateTime due ? (now - due).TotalMinutes : 0;
            bool alert = oldestMinutes > _options.BacklogAgeAlertMinutes || deadLetters > 0;
            return new OutboxHealth(oldestMinutes, deadLetters, alert);
        }
    }
}
