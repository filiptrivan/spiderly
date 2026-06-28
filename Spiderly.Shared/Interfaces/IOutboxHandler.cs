using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Consumes one kind of outbox row. The framework sweep (<c>OutboxDispatcherJob&lt;TOutbox&gt;</c>)
    /// resolves the handler whose <see cref="Code"/> matches the row's <see cref="IOutboxMessage.HandlerCode"/>
    /// and hands it the row's payload.
    ///
    /// <para>Register one implementation per code in DI (e.g. <c>services.AddScoped&lt;IOutboxHandler, MyHandler&gt;()</c>).
    /// Adding a new kind of deferred work = write one handler + register it; no framework change. This is the
    /// open-extensible replacement for a hard-coded dispatch switch.</para>
    /// <example>
    /// <code>
    /// public class WingsExportOutboxHandler : IOutboxHandler
    /// {
    ///     public string Code => "WingsExport";
    ///     public Task HandleAsync(string payload, CancellationToken ct)
    ///     {
    ///         WingsPayload p = JsonSerializer.Deserialize&lt;WingsPayload&gt;(payload);
    ///         _backgroundJobClient.Enqueue&lt;WingsOrderExportJob&gt;(j =&gt; j.ExportAsync(p.OrderId));
    ///         return Task.CompletedTask;
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public interface IOutboxHandler
    {
        /// <summary>Stable code matched against <see cref="IOutboxMessage.HandlerCode"/>. Must be unique across registered handlers.</summary>
        string Code { get; }

        /// <summary>
        /// Retry tuning for this handler's rows — how many times <c>OutboxDispatcherJob</c> retries before dead-lettering,
        /// and the backoff ceiling. Override to let loss-tolerant work retry longer, or latency-sensitive work give up
        /// sooner. Defaults to <see cref="OutboxRetryPolicy.Default"/> (12 attempts, 1-hour backoff cap).
        /// </summary>
        OutboxRetryPolicy RetryPolicy => OutboxRetryPolicy.Default;

        /// <summary>
        /// Performs the deferred work for one outbox row. Throwing marks the row as a failed attempt
        /// (it stays pending and is retried on a later sweep, up to the retry cap). Must be idempotent —
        /// a crash between handling and marking the row dispatched can replay it.
        /// </summary>
        /// <param name="payload">The row's <see cref="IOutboxMessage.Payload"/> (handler-specific JSON).</param>
        /// <param name="cancellationToken">Cancellation token for the sweep.</param>
        Task HandleAsync(string payload, CancellationToken cancellationToken);
    }
}
