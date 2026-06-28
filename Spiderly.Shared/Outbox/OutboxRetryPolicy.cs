namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// Per-handler retry tuning for the transactional outbox. An <see cref="Spiderly.Shared.Interfaces.IOutboxHandler"/>
    /// returns one of these from <c>RetryPolicy</c> to control how many times <c>OutboxDispatcherJob</c> retries its rows
    /// and how far apart — so loss-tolerant, idempotent work (e.g. a job hand-off to a flaky ERP) can retry longer than
    /// latency-sensitive work (e.g. a customer email). Handlers that don't override it use <see cref="Default"/>.
    /// </summary>
    /// <param name="MaxAttempts">Failed attempts before the row is dead-lettered (left for an admin to Retry/Dismiss).</param>
    /// <param name="MaxBackoff">Ceiling for the exponential backoff between attempts (1, 2, 4, … minutes, capped here).</param>
    public sealed record OutboxRetryPolicy(int MaxAttempts, TimeSpan MaxBackoff)
    {
        /// <summary>The policy used by any handler that does not override <c>IOutboxHandler.RetryPolicy</c>: 12 attempts, 1-hour backoff cap (~6 h total window).</summary>
        public static readonly OutboxRetryPolicy Default = new(MaxAttempts: 12, MaxBackoff: TimeSpan.FromHours(1));
    }
}
