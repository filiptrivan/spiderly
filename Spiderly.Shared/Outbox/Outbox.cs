using System.Text.Json;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// Default <see cref="IOutbox"/> implementation, generic over the consumer's concrete outbox entity
    /// <typeparamref name="TOutbox"/>. Registered by <c>AddOutbox&lt;TOutbox&gt;()</c> with the scaffolded
    /// entity type, so framework code can stage rows without depending on the consumer assembly.
    /// </summary>
    /// <typeparam name="TOutbox">The consumer's <c>[SpiderlyEntity]</c> implementing <see cref="IOutboxMessage"/>.</typeparam>
    public class Outbox<TOutbox> : IOutbox
        where TOutbox : class, IOutboxMessage, new()
    {
        private readonly IApplicationDbContext _context;

        /// <summary>Creates the outbox over the request/job-scoped <see cref="IApplicationDbContext"/>.</summary>
        public Outbox(IApplicationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public void Enqueue(string handlerCode, object payload)
        {
            // CreatedAt / Version are stamped by ApplicationDbContext.SaveChangesAsync on insert.
            _context.DbSet<TOutbox>().Add(new TOutbox
            {
                HandlerCode = handlerCode,
                Payload = JsonSerializer.Serialize(payload),
                AttemptCount = 0,
            });
        }
    }
}
