namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Enqueues transactional-outbox rows. The row is added to the current <see cref="IApplicationDbContext"/>
    /// change tracker, so it commits (or rolls back) atomically with the surrounding entity write. The
    /// generated CRUD operation flushes the tracker before commit — in both save and delete hooks — so no
    /// manual <c>SaveChangesAsync</c> is needed when enqueuing from a hook.
    ///
    /// <para><b>INVARIANT</b> — call <see cref="Enqueue"/> inside a <c>WithTransactionAsync</c> block, after the
    /// entity write it depends on. Called outside a transaction, the row commits in its own implicit transaction
    /// and the atomicity guarantee is lost. (No runtime guard — enforced by review/tests, same as the rest of the
    /// transactional seam.)</para>
    /// </summary>
    public interface IOutbox
    {
        /// <summary>
        /// Stages an outbox row for the handler identified by <paramref name="handlerCode"/>. The row is not sent
        /// now; a recurring sweep dispatches it after commit. <paramref name="payload"/> is JSON-serialized and
        /// should carry semantic intent (ids), not rendered content.
        /// </summary>
        /// <param name="handlerCode">Matches the <see cref="IOutboxHandler.Code"/> that will consume the row.</param>
        /// <param name="payload">Handler-specific data; serialized to JSON.</param>
        void Enqueue(string handlerCode, object payload);
    }
}
