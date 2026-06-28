namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Contract for the transactional-outbox row. The concrete entity is scaffolded into the
    /// consumer app (a <c>[SpiderlyEntity]</c> deriving <c>BusinessObject&lt;long&gt;</c> and implementing
    /// this interface), so it gets a generated admin page and migration; the framework outbox engine
    /// (<see cref="IOutbox"/>, <c>OutboxDispatcherJob&lt;TOutbox&gt;</c>) is generic over the concrete type
    /// constrained to this interface — mirroring how <c>ApplicationDbContext&lt;TUser&gt;</c> is generic
    /// over <c>IUser</c>.
    ///
    /// <para><c>Id</c> and <c>CreatedAt</c> are satisfied by <c>BusinessObject&lt;long&gt;</c> on the
    /// scaffolded entity; they are declared here so the generic engine can order the sweep by
    /// enqueue time.</para>
    /// </summary>
    public interface IOutboxMessage
    {
        /// <summary>Primary key (satisfied by <c>BusinessObject&lt;long&gt;.Id</c> on the scaffolded entity).</summary>
        long Id { get; set; }

        /// <summary>Enqueue time, set by the DbContext on insert (satisfied by <c>BusinessObject&lt;long&gt;.CreatedAt</c>). The sweep dispatches oldest-first.</summary>
        DateTime CreatedAt { get; set; }

        /// <summary>Identifies which <see cref="IOutboxHandler"/> consumes this row. Open-ended string, not a framework enum.</summary>
        string HandlerCode { get; set; }

        /// <summary>Handler-specific JSON payload. Carries semantic intent (e.g. ids), not rendered content.</summary>
        string Payload { get; set; }

        /// <summary>Set once the row has been handled (or dismissed). Pending rows have <c>null</c>.</summary>
        DateTime? DispatchedAt { get; set; }

        /// <summary>Number of failed dispatch attempts; the sweep stops retrying past a cap.</summary>
        int AttemptCount { get; set; }

        /// <summary>Timestamp of the last dispatch attempt.</summary>
        DateTime? LastAttemptedAt { get; set; }

        /// <summary>Truncated exception/error message from the last failed attempt.</summary>
        string LastError { get; set; }

        /// <summary>When set, the sweep skips this row until this time — exponential backoff between failed attempts, set by <c>OutboxDispatcherJob</c>. <c>null</c> means eligible immediately (a fresh row).</summary>
        DateTime? NextAttemptAt { get; set; }

        /// <summary>Set by the admin Dismiss action — the operator who marked this row handled out-of-band. <see cref="DispatchedAt"/> is set in the same write so the sweep skips it.</summary>
        long? DismissedByUserId { get; set; }
    }
}
