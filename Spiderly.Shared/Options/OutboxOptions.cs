using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared
{
    /// <summary>
    /// Optional runtime tuning for the transactional-outbox retry behavior, bound from
    /// <c>AppSettings:Spiderly.Shared:Outbox</c>. Everything is optional: an unset value falls through to the
    /// handler's code-declared <see cref="IOutboxHandler.RetryPolicy"/> (which itself defaults to
    /// <see cref="OutboxRetryPolicy.Default"/>). Lets ops retune outbox retries without a redeploy.
    /// </summary>
    public class OutboxOptions
    {
        /// <summary>Global override applied to handlers that don't declare their own policy. Null (or null fields) keeps the framework default <see cref="OutboxRetryPolicy.Default"/>.</summary>
        public OutboxRetryOptions Default { get; set; }

        /// <summary>Per-handler-code overrides, keyed by <see cref="IOutboxHandler.Code"/> (e.g. "WingsExport"). An entry wins over both the global <see cref="Default"/> and the handler's code-declared policy.</summary>
        public Dictionary<string, OutboxRetryOptions> Handlers { get; set; } = new();

        /// <summary>Days to keep fully-handled (<c>DispatchedAt</c> set) outbox rows before <c>OutboxRetentionJob</c> purges them. Pending/dead-lettered rows are never purged. Default 30.</summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>The outbox health check (<c>OutboxHealthJob</c>) logs an error — for your alerting — when the oldest still-due pending row is older than this many minutes. Default 15.</summary>
        public int BacklogAgeAlertMinutes { get; set; } = 15;
    }

    /// <summary>A retry-policy override. Fields are nullable so an unset field falls through to the next layer instead of binding to 0.</summary>
    public class OutboxRetryOptions
    {
        /// <summary>Failed attempts before a row is dead-lettered.</summary>
        public int? MaxAttempts { get; set; }

        /// <summary>Ceiling (in minutes) for the exponential backoff between attempts.</summary>
        public int? MaxBackoffMinutes { get; set; }
    }
}
