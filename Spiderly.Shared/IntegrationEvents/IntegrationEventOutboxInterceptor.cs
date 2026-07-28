using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.IntegrationEvents
{
    /// <summary>
    /// Harvests <see cref="IIntegrationEvent"/>s raised on tracked entities into the transactional outbox, in the same
    /// transaction as the entity write — so the consumer never has to remember a manual publish, and the event rolls
    /// back with the command if it throws. Generic over the consumer's concrete outbox entity <typeparamref name="TOutbox"/>.
    /// Registered as a singleton <c>ISaveChangesInterceptor</c> and picked up by <c>SpiderlyAddDbContext</c>.
    ///
    /// <para>Harvest runs <b>after</b> the save assigns store-generated ids, so each event's
    /// <see cref="IntegrationEvent.AggregateId"/> can be stamped from the raising entity's now-known id. It reads each
    /// event's stable code straight off the type (<see cref="OutboxEnvelope.For"/> → <see cref="OutboxCode.Of"/>) and
    /// stages rows via the shared <see cref="Outbox{TOutbox}"/>. It looks up the event's registered handlers and stages
    /// <b>one row per handler</b> (each tagged with the handler's <see cref="IIntegrationEventHandler.Code"/>), so a
    /// single handler's failure retries only its own row — not the whole event. The staged rows are flushed by a second
    /// save inside the surrounding transaction (guarded by <see cref="_harvesting"/> so it doesn't re-harvest).</para>
    /// </summary>
    /// <typeparam name="TOutbox">The consumer's <c>[SpiderlyEntity]</c> implementing <see cref="IOutboxMessage"/>.</typeparam>
    public class IntegrationEventOutboxInterceptor<TOutbox> : SaveChangesInterceptor
        where TOutbox : class, IOutboxMessage, new()
    {
        // Marks the re-entrant second save (which persists the harvested rows) so it doesn't harvest again. AsyncLocal —
        // not an instance field — because the interceptor is a singleton shared across requests/threads.
        private static readonly AsyncLocal<bool> _harvesting = new();

        private readonly ILogger<IntegrationEventOutboxInterceptor<TOutbox>> _logger;
        private readonly CodeTypeRegistry<IIntegrationEvent> _registry;
        // event type -> codes of its registered handlers; built once on first harvest (handlers known only at runtime).
        private readonly Lazy<IReadOnlyDictionary<Type, IReadOnlyList<string>>> _handlerCodesByEventType;

        /// <summary>Creates the interceptor.</summary>
        public IntegrationEventOutboxInterceptor(
            ILogger<IntegrationEventOutboxInterceptor<TOutbox>> logger,
            CodeTypeRegistry<IIntegrationEvent> registry,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _registry = registry;
            _handlerCodesByEventType = new Lazy<IReadOnlyDictionary<Type, IReadOnlyList<string>>>(
                () => BuildHandlerMap(scopeFactory), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <inheritdoc/>
        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            int staged = Stage(eventData, out DbContext? context);
            if (staged == 0)
                return result;

            _harvesting.Value = true;
            try
            {
                await context!.SaveChangesAsync(cancellationToken); // staged > 0 implies Stage saw a non-null context
            }
            finally
            {
                _harvesting.Value = false;
            }

            _logger.LogDebug("Harvested {Count} integration event(s) to the outbox.", staged);
            return result;
        }

        /// <inheritdoc/>
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            int staged = Stage(eventData, out DbContext? context);
            if (staged == 0)
                return result;

            _harvesting.Value = true;
            try
            {
                context!.SaveChanges(); // staged > 0 implies Stage saw a non-null context
            }
            finally
            {
                _harvesting.Value = false;
            }

            _logger.LogDebug("Harvested {Count} integration event(s) to the outbox.", staged);
            return result;
        }

        // Guard + harvest, shared by the sync/async overrides (which differ only in the flush call). Returns the number
        // of outbox rows staged (0 = nothing to flush) and the context to flush.
        private int Stage(SaveChangesCompletedEventData eventData, out DbContext? context)
        {
            context = eventData.Context;
            if (context == null || _harvesting.Value)
                return 0;

            return StageIntegrationEvents(context);
        }

        // Adds one outbox row per raised event (stamping AggregateId from the now-assigned entity id) and clears the
        // entities' pending events. Does NOT save — the caller flushes inside the same transaction. Returns the count.
        private int StageIntegrationEvents(DbContext context)
        {
            ChangeTracker tracker = context.ChangeTracker;

            // Auto change-detection off for the scan: the save just completed (nothing has changed since), and the
            // harvest only reads each entity's [NotMapped] IntegrationEvents collection (which DetectChanges never
            // tracks). Otherwise Entries<T>() would re-run a full DetectChanges over the whole graph on EVERY save —
            // this interceptor runs after every SaveChanges app-wide, almost always with no event to harvest.
            bool autoDetect = tracker.AutoDetectChangesEnabled;
            tracker.AutoDetectChangesEnabled = false;
            try
            {
                // Collect the raisers first (allocating only when one is actually found) — we can't Add rows while
                // enumerating the change tracker.
                List<EntityEntry<IHasIntegrationEvents>>? raisers = null;
                foreach (EntityEntry<IHasIntegrationEvents> entry in tracker.Entries<IHasIntegrationEvents>())
                    if (entry.Entity.IntegrationEvents.Count > 0)
                        (raisers ??= new()).Add(entry);

                if (raisers == null)
                    return 0;

                // The rows staged below are flushed by a SECOND save (in the caller) inside the SAME transaction.
                // Without an ambient transaction that second save isn't atomic with the entity write — which already
                // committed in the first save's implicit transaction — so a crash between them would lose the event.
                // Fail loud rather than silently breaking the outbox's exactly-once guarantee. Checked before any
                // staging so nothing is mutated on the throw path. Gated on IsRelational(): only a transactional store
                // has a transaction to honor and a crash window to protect — a non-relational provider (e.g. in-memory)
                // has no atomicity to break and no transaction to begin.
                if (context.Database.IsRelational() && context.Database.CurrentTransaction == null)
                    throw new InvalidOperationException(
                        "Integration events were raised but SaveChanges ran outside a transaction, so the outbox row " +
                        "cannot commit atomically with the entity write. Wrap the command in " +
                        "IApplicationDbContext.WithTransactionAsync(...).");

                Outbox<TOutbox> outbox = new((IApplicationDbContext)context);
                int staged = 0;
                foreach (EntityEntry<IHasIntegrationEvents> entry in raisers)
                {
                    long aggregateId = GetAggregateId(entry);

                    foreach (IIntegrationEvent integrationEvent in entry.Entity.IntegrationEvents)
                    {
                        if (integrationEvent is IntegrationEvent baseEvent && baseEvent.AggregateId == 0)
                            baseEvent.AggregateId = aggregateId;

                        OutboxEnvelope envelope = OutboxEnvelope.For(integrationEvent);

                        // The producer reads [OutboxCode] straight off the type, so an event never passed to
                        // AddIntegrationEvents(...) stages fine here but can't be resolved at delivery — a poison row that
                        // dead-letters silently. Preflight against the delivery registry and fail loud at the raise site
                        // (the throw rolls the command back inside the transaction guarded above) instead.
                        if (!_registry.IsRegistered(envelope.Code))
                            throw new InvalidOperationException(
                                $"Integration event '{integrationEvent.GetType().Name}' ([OutboxCode(\"{envelope.Code}\")]) was " +
                                $"raised but is not registered, so it could never be delivered. Call " +
                                $"spiderly.AddIntegrationEvents(typeof({integrationEvent.GetType().Name})).");

                        // Fan out: one outbox row per registered handler, each addressed by the handler's Code — so a
                        // single handler's failure retries only its own row, not the whole event. No handlers → no rows.
                        IReadOnlyList<string> handlerCodes =
                            _handlerCodesByEventType.Value.TryGetValue(integrationEvent.GetType(), out IReadOnlyList<string>? codes)
                                ? codes
                                : Array.Empty<string>();

                        if (handlerCodes.Count == 0)
                            _logger.LogWarning(
                                "Integration event '{EventCode}' has no registered IIntegrationEventHandler — harvested no rows.",
                                envelope.Code);

                        foreach (string handlerCode in handlerCodes)
                        {
                            outbox.Enqueue(IntegrationEventOutboxHandler.HandlerCode, new IntegrationEventOutboxPayload
                            {
                                Code = envelope.Code,
                                Data = envelope.Data,
                                TargetHandlerCode = handlerCode,
                            });
                            staged++;
                        }
                    }

                    entry.Entity.ClearIntegrationEvents();
                }

                return staged;
            }
            finally
            {
                tracker.AutoDetectChangesEnabled = autoDetect;
            }
        }

        // event type -> the Codes of its registered handlers. Built once (lazily, first harvest) from the registered
        // IIntegrationEventHandlers, since handlers are only resolvable at runtime. Dup code per event type = config bug.
        private static IReadOnlyDictionary<Type, IReadOnlyList<string>> BuildHandlerMap(IServiceScopeFactory scopeFactory)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            Dictionary<Type, List<string>> map = new();
            foreach (IIntegrationEventHandler handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler>())
            {
                if (!map.TryGetValue(handler.EventType, out List<string>? codes))
                    map[handler.EventType] = codes = new();
                if (codes.Contains(handler.Code))
                    throw new InvalidOperationException(
                        $"Duplicate IIntegrationEventHandler.Code '{handler.Code}' for event '{handler.EventType.Name}'. Codes must be unique per event type.");
                codes.Add(handler.Code);
            }
            return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
        }

        // The raising entity's single-column primary key as a long (events are stamped with it). Spiderly ids are
        // integral (int/long/byte — SPIDERLY018), but IHasIntegrationEvents is unconstrained, so a Guid/string single-
        // column key is structurally possible; widen only integral keys and leave anything else (incl. null/composite/
        // absent) at 0 — the event simply carries no aggregate id — rather than throwing from inside the post-save harvest.
        private static long GetAggregateId(EntityEntry entry)
        {
            IReadOnlyList<IProperty>? keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
            if (keyProperties == null || keyProperties.Count != 1)
                return 0;

            object? value = entry.Property(keyProperties[0].Name).CurrentValue;
            return value switch
            {
                byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToInt64(value),
                _ => 0,
            };
        }
    }
}
