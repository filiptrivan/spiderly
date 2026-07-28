using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// Recurring sweep that delivers pending transactional-outbox rows. Generic over the consumer's concrete
    /// outbox entity <typeparamref name="TOutbox"/>; scheduled by <c>SpiderlyUseOutboxRecurringJob&lt;TOutbox&gt;()</c>.
    ///
    /// <para><c>[AutomaticRetry(Attempts = 0)]</c> — retry semantics live on the row (<see cref="IOutboxMessage.AttemptCount"/>,
    /// with per-handler exponential backoff staged via <see cref="IOutboxMessage.NextAttemptAt"/> using each handler's
    /// <see cref="IOutboxHandler.RetryPolicy"/>) plus the recurring schedule, not on the Hangfire job; a recurring job
    /// that also retries would multiply runs.</para>
    ///
    /// <para><c>[DisableConcurrentExecution]</c> — the sweep reads pending rows without a row-level claim, so two
    /// overlapping sweeps would read and dispatch the same rows (duplicate side effects). The interval is one minute,
    /// but a batch of slow handlers can overrun it; the distributed lock (held in Hangfire storage, so it also covers
    /// multiple backend instances) serializes runs. This makes dispatch strictly serial across the fleet — the
    /// throughput ceiling, and deliberate: to scale, swap the global lock for <c>SELECT … FOR UPDATE SKIP LOCKED</c>
    /// + concurrent workers (needed only at high volume; not a bug). The timeout is generous on purpose: an overrunning tick <i>waits</i>
    /// for the in-flight sweep to release the lock and then runs, rather than failing — so a slow batch does not raise a
    /// spurious failure alert via the job-failed filter. Only a sweep stuck past the timeout fails, which is a genuine
    /// "outbox is wedged" signal worth surfacing.</para>
    ///
    /// <para>Per-row <c>SaveChanges</c> narrows the crash window: a process crash mid-batch must not replay rows already
    /// marked dispatched. The accepted residual window is a handler that completes its side effect, then the process
    /// crashes before the row is marked dispatched → the row is retried, so handlers must be idempotent.</para>
    ///
    /// <para>Each handler runs in its own DI scope, so a handler's <see cref="IApplicationDbContext"/> (and any
    /// entities it mutates) is isolated from the dispatcher's context. The dispatcher persists only the outbox row's
    /// own state — never a handler's half-finished entity writes from a failed attempt. The bookkeeping save is
    /// isolated per row so one row's persistence failure (e.g. a concurrency conflict with an admin Retry) does not
    /// abort the rest of the batch.</para>
    /// </summary>
    /// <typeparam name="TOutbox">The consumer's <c>[SpiderlyEntity]</c> implementing <see cref="IOutboxMessage"/>.</typeparam>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 10 * 60)]
    public class OutboxDispatcherJob<TOutbox>
        where TOutbox : class, IOutboxMessage, new()
    {
        private const int BatchSize = 100;
        private const int LastErrorMaxLength = 2000;

        private readonly IApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxDispatcherJob<TOutbox>> _logger;
        private readonly OutboxOptions _options;

        /// <summary>Creates the sweep over the job-scoped context and a scope factory used to isolate each handler.</summary>
        public OutboxDispatcherJob(
            IApplicationDbContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxDispatcherJob<TOutbox>> logger,
            IOptions<OutboxOptions> options)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        /// <summary>Claims a batch of pending rows (oldest first, under the retry cap) and dispatches each to its handler.</summary>
        public async Task ProcessAsync()
        {
            // Per-handler retry caps are enforced in the loop below (each row's cap depends on its handler's
            // RetryPolicy, which isn't known to this query); a row that crosses its cap is dead-lettered by parking
            // NextAttemptAt at NeverRetry, so the time gate here is the single eligibility filter.
            DateTime now = DateTime.UtcNow;
            List<TOutbox> pending = await _context.DbSet<TOutbox>()
                .Where(x => x.DispatchedAt == null
                    && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(BatchSize)
                .ToListAsync();

            if (pending.Count == 0)
                return;

            _logger.LogDebug("Outbox sweep: {Count} pending messages", pending.Count);

            // Validate handler registration once, up front, so a duplicate Code fails loud (propagates out of the
            // sweep) instead of being swallowed as a per-row dispatch error inside the loop below.
            using (IServiceScope validationScope = _scopeFactory.CreateScope())
                BuildHandlerLookup(validationScope.ServiceProvider.GetServices<IOutboxHandler>());

            foreach (TOutbox msg in pending)
            {
                // Default until the handler is resolved below; if the row's HandlerCode has no registered handler the
                // dispatch throws before this is set, and the row retries under the default policy.
                OutboxRetryPolicy policy = OutboxRetryPolicy.Default;
                try
                {
                    // Resolve and run the handler in its own DI scope so its context is isolated from the
                    // dispatcher's — a failed attempt that mutated entities must not have those writes flushed
                    // by the dispatcher's bookkeeping save below.
                    using IServiceScope scope = _scopeFactory.CreateScope();

                    // Rebuilt per row by design, not an "hoist it out of the loop" miss: handlers are scoped to this
                    // per-row scope (which isolates each attempt's DbContext), so their instances can't be reused
                    // across rows. The cost — resolving a tiny, fixed handler set (one per kind of deferred work, not
                    // per row) plus a few-entry dictionary — is microseconds, dwarfed by the per-row DB round-trips
                    // this loop already pays (the handler's work + the bookkeeping SaveChanges below).
                    Dictionary<string, IOutboxHandler> handlersByCode =
                        BuildHandlerLookup(scope.ServiceProvider.GetServices<IOutboxHandler>());

                    if (!handlersByCode.TryGetValue(msg.HandlerCode, out IOutboxHandler? handler))
                        throw new InvalidOperationException(
                            $"No IOutboxHandler registered for HandlerCode '{msg.HandlerCode}'.");

                    policy = ResolvePolicy(handler);
                    await handler.HandleAsync(msg.Payload, CancellationToken.None);
                    msg.DispatchedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    msg.AttemptCount++;
                    msg.LastAttemptedAt = DateTime.UtcNow;
                    msg.LastError = ex.Message.Length > LastErrorMaxLength
                        ? ex.Message[..LastErrorMaxLength]
                        : ex.Message;

                    if (msg.AttemptCount >= policy.MaxAttempts)
                    {
                        // Crossed the handler's retry cap — park NextAttemptAt far in the future so the time-based sweep
                        // skips it forever (AttemptCount stays truthful for the admin view). Logged at Error (→ Sentry)
                        // as the proactive dev signal; the row stays as the admin-facing record, and an admin Retry
                        // clears NextAttemptAt back to null to requeue it. Fires once, on the attempt that hits the cap.
                        msg.NextAttemptAt = OutboxRetryPolicy.NeverRetry;
                        _logger.LogError(ex,
                            "Outbox message {MessageId} (handler={HandlerCode}) permanently failed after {MaxAttempts} attempts and will not be retried.",
                            msg.Id, msg.HandlerCode, policy.MaxAttempts);
                    }
                    else
                    {
                        // Exponential backoff: defer the next attempt so a transient downstream outage isn't hammered
                        // and doesn't burn through the retry cap in minutes. The sweep skips this row until NextAttemptAt.
                        msg.NextAttemptAt = msg.LastAttemptedAt + BackoffDelay(msg.AttemptCount, policy.MaxBackoff);
                        _logger.LogError(ex,
                            "Outbox dispatch failed for message {MessageId} (handler={HandlerCode}, attempt={Attempt}); next attempt at {NextAttemptAt:o}",
                            msg.Id, msg.HandlerCode, msg.AttemptCount, msg.NextAttemptAt);
                    }
                }

                await PersistRowStateAsync(msg);
            }
        }

        // Per-row save (see class remarks). Isolated so one row's persistence failure — e.g. a
        // DbUpdateConcurrencyException from an admin Retry touching the row mid-sweep — does not abort the rest of
        // the batch. The failed entry is detached so it can't poison the next row's save; the row stays as-is in the
        // database and the next sweep retries it.
        private async Task PersistRowStateAsync(TOutbox msg)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Outbox: failed to persist state for message {MessageId}; continuing with the rest of the batch.",
                    msg.Id);

                foreach (EntityEntry<TOutbox> entry in _context.ChangeTracker.Entries<TOutbox>()
                    .Where(e => ReferenceEquals(e.Entity, msg))
                    .ToList())
                    entry.State = EntityState.Detached;
            }
        }

        // Effective retry policy for a handler: an explicit per-handler config override wins; otherwise a handler that
        // declared its own (non-default) policy keeps it; otherwise the global config default, else the framework default.
        private OutboxRetryPolicy ResolvePolicy(IOutboxHandler handler)
        {
            if (_options.Handlers != null
                && _options.Handlers.TryGetValue(handler.Code, out OutboxRetryOptions? perHandler)
                && perHandler != null)
                return Apply(perHandler, handler.RetryPolicy);

            if (handler.RetryPolicy != OutboxRetryPolicy.Default)
                return handler.RetryPolicy;

            return _options.Default != null
                ? Apply(_options.Default, OutboxRetryPolicy.Default)
                : OutboxRetryPolicy.Default;
        }

        // Overlays the configured (nullable) fields onto a fallback policy, so a partial override (e.g. only
        // MaxAttempts) keeps the fallback's other values.
        private static OutboxRetryPolicy Apply(OutboxRetryOptions o, OutboxRetryPolicy fallback) =>
            new(
                o.MaxAttempts ?? fallback.MaxAttempts,
                o.MaxBackoffMinutes is int m ? TimeSpan.FromMinutes(m) : fallback.MaxBackoff);

        // Exponential backoff for the next retry: 1, 2, 4, 8, 16, 32 minutes, then capped at the handler's maxBackoff.
        // attemptCount is the already-incremented count of failed attempts.
        private static TimeSpan BackoffDelay(int attemptCount, TimeSpan maxBackoff)
        {
            double minutes = Math.Min(Math.Pow(2, attemptCount - 1), maxBackoff.TotalMinutes);
            return TimeSpan.FromMinutes(minutes);
        }

        // A duplicate Code is a registration bug — fail loud with the offending code rather than
        // silently letting ToDictionary throw an opaque "same key" ArgumentException.
        private static Dictionary<string, IOutboxHandler> BuildHandlerLookup(IEnumerable<IOutboxHandler> handlers)
        {
            Dictionary<string, IOutboxHandler> map = new();
            foreach (IOutboxHandler handler in handlers)
            {
                if (!map.TryAdd(handler.Code, handler))
                    throw new InvalidOperationException(
                        $"Duplicate IOutboxHandler.Code '{handler.Code}' registered " +
                        $"({map[handler.Code].GetType().Name} and {handler.GetType().Name}). Codes must be unique.");
            }
            return map;
        }
    }
}
