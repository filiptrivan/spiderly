namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Reacts to an <see cref="IIntegrationEvent"/> after the raising transaction commits. Register one (or many)
    /// per event type in DI (<c>services.AddScoped&lt;IIntegrationEventHandler, MyHandler&gt;()</c>); the
    /// <c>IntegrationEventOutboxHandler</c> resolves every handler whose <see cref="EventType"/> matches the
    /// delivered event and invokes each. Extend
    /// <see cref="Spiderly.Shared.IntegrationEvents.IntegrationEventHandler{TEvent}"/> for a typed implementation
    /// rather than implementing this non-generic form directly.
    ///
    /// <para>Each handler gets its OWN outbox row (harvest fans out one row per handler), so a handler's failure
    /// retries only that handler — never its siblings. Handlers run post-commit, each in its own DI scope, and should
    /// still be idempotent for the narrow crash window between a successful side effect and the row being marked
    /// dispatched (which replays that one handler's row).</para>
    /// </summary>
    public interface IIntegrationEventHandler
    {
        /// <summary>The event type this handler reacts to; used to match it to a delivered event.</summary>
        Type EventType { get; }

        /// <summary>
        /// Stable identity for this handler. Harvest writes one outbox row per handler tagged with this code, and
        /// delivery resolves the row back to exactly this handler — so each retries in isolation. Must be unique among
        /// handlers of the same <see cref="EventType"/>. The typed base defaults it to the type name; override with a
        /// constant if the class may be renamed (an in-flight row carries the old code and would dead-letter).
        /// </summary>
        string Code { get; }

        /// <summary>Performs the reaction. Throwing causes the outbox row to be retried.</summary>
        /// <param name="integrationEvent">The rebuilt event (cast to the concrete type by the typed base).</param>
        /// <param name="cancellationToken">Cancellation token for the outbox sweep.</param>
        Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
    }
}
