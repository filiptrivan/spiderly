# Spiderly Integration Events — Design

> Status: **Slices 1–3 IMPLEMENTED** — framework core in `Spiderly.Shared/IntegrationEvents/` (build clean + 7 unit tests green) and the first PACMS consumer (`OrderCreated` + a storefront-revalidation handler; PACMS.Business + PACMS.WebAPI build clean). Slice 4 (unify the shared core with notifications) is **designed & ready to build** — `docs/outbox-core-unification.md`. A typed, durable, post-commit event bus: an aggregate **raises** an `IIntegrationEvent`, a `SaveChangesInterceptor` **harvests** it into the transactional outbox in the same transaction, and after commit the single `IntegrationEventOutboxHandler` delivers each row to its one handler.
>
> **Per-handler retry rows** — harvest fans out **one outbox row per registered handler** (each addressed by the handler's `Code`), so a single handler's failure retries only its own row, never its siblings — matching how `Notifier` stages one row per channel. (Initial slices shipped bundled one-row-per-event dispatch; this was the planned upgrade.) Handlers should still be idempotent for the narrow crash-replay window — the existing `IOutboxHandler` contract.
>
> This **generalizes the notification framework** (`docs/notification-framework.md`): a notification is the special case of an event whose subscribers are *delivery channels*. This doc deliberately reuses that machinery's shapes (type registry, code attribute, per-target outbox rows, the single framework `IOutboxHandler`) rather than inventing a parallel system. It also subsumes the **recipient-kind** limitation ([filiptrivan/spiderly#…](https://github.com/filiptrivan/spiderly) — the `FirstOrDefault()` resolver) by replacing opaque-id resolution with typed handlers.
>
> Reference implementation to be extracted from: PACMS (`prodavnicaalata.rs`). First consumer = an `OrderCreated` event; the existing `OrderOutboxStaging.EnqueueOrderConfirmation` becomes one `IIntegrationEventHandler<OrderCreated>`.

## Problem

A Spiderly app needs *business reactions* to a domain fact: "when an order is created, reindex it for search, push it to the ERP, emit an analytics event." These reactions are:

- **post-commit** — they must run only after the order is durably committed, and must **not** roll the order back if they fail;
- **independent** — one failing must not block or replay the others;
- **open-ended** — a consumer (or a future module) adds a reaction without the publisher knowing about it.

Spiderly has no native path for this today. The two existing seams each fall short:

- **`INotifier` / notifications** fan out 1→N, but only to **delivery channels** (`INotificationChannel.SendAsync` renders content and ships it to a human/external sink). It can't trigger arbitrary business code.
- **`IOutbox` / `IOutboxHandler`** is durable and transactional, but **1 code → 1 handler** (`OutboxDispatcherJob` throws on a duplicate `Code`). "OrderCreated triggers 3 reactions" has no native shape — the consumer hand-wires an `IEnumerable<ISubscriber>` inside one handler, and the three then share **one** outbox row's retry (one fails → all replay).

A third gap is *ergonomic*: today the publish is a call the service must remember (`_outboxStaging.EnqueueOrderConfirmation(id)`). A refactor can silently drop it; nothing ties "an order was created" to "the OrderCreated fact exists."

## Two tiers — and we ship only one

A transactional command has two kinds of reactions with **opposite** requirements. Naming them apart is load-bearing:

| | **Domain reaction** (atomic) | **Integration reaction** (post-commit) |
|---|---|---|
| e.g. | stock decrement, loyalty award, discount usage | search reindex, ERP push, analytics, the confirmation email |
| consistency | strong — same transaction | eventual |
| on failure | roll the command back | retry in isolation; never roll the command back |
| vehicle | **in-process, synchronous, pre-commit** | **transactional outbox, post-commit** |

**This doc builds only the integration tier.** Atomic reactions stay as plain inline domain logic inside the command (what PACMS's `OrderIntake` does today) — routing them through any event bus would forfeit their atomicity and is an explicit anti-goal. The synchronous in-process **domain-event** tier is *named and reserved* (`IDomainEvent`) but not built; see [Naming rationale](#naming-rationale).

## Design

### Component map — each piece is a sibling of an existing notification piece

| New type | Role | Mirrors |
|---|---|---|
| `IIntegrationEvent` | Marker for a serializable "something happened" fact; carries the data its handlers need | `INotification` |
| `IntegrationEvent` (abstract) | Convenience base exposing `long AggregateId` (stamped by the harvester) | `EmailRenderer<T>` (convenience base pattern) |
| `[IntegrationEventCode("OrderCreated")]` | Stable code ↔ type; survives rename; serialized into the outbox row | `[NotificationCode]` |
| `IIntegrationEventHandler` (+ typed base `IntegrationEventHandler<TEvent>`) | A subscriber: `Type EventType { get; }` + `Task HandleAsync(IIntegrationEvent, CancellationToken)`; dispatch matches handlers by `EventType` — no reflection | `IEmailRenderer` + `EmailRenderer<T>` |
| `IntegrationEventTypeRegistry` | code ↔ `Type`, built by assembly scan, dup-code fails loud; rebuilds the event at delivery | `NotificationTypeRegistry` |
| ~~`IntegrationEventHandlerRegistry`~~ | **v2 only** — needed solely for per-handler rows; v1 resolves handlers at dispatch time, so this is not built | *(deferred)* |
| `IntegrationEventOutboxInterceptor<TOutbox>` | `SaveChangesInterceptor`; harvests raised events from tracked entities → writes **one outbox row per event** in the same transaction (AsyncLocal-guarded second save) | *(new — the "can't forget to publish" piece)* |
| `IntegrationEventOutboxPayload` | `{ EventCode, EventData }` (gains `HandlerCode` in v2) | `NotificationOutboxPayload` `{ NotificationCode, NotificationData, RecipientId, ChannelCode }` |
| `IntegrationEventOutboxHandler` | The **single** framework `IOutboxHandler` (`Code = "IntegrationEvent"`) consuming those rows; rebuilds the event, resolves every handler whose `EventType` matches, invokes each (isolating failures, then throwing so the row retries) | `NotificationOutboxHandler` (`Code = "Notification"`) |
| ~~`IIntegrationEventPublisher`~~ | **v2 / optional** — escape hatch for events not anchored to a tracked aggregate; not built in v1 (aggregate-raise covers the first consumer) | *(deferred)* |
| `spiderly.AddIntegrationEvents(params markers)` | DI setup: type registry + interceptor + the one outbox handler | `AddNotifications()` |

Entity-side (an `IHasIntegrationEvents` interface implemented by the entity base `BusinessObject<T>`, collection `[NotMapped]`):

```csharp
public interface IHasIntegrationEvents
{
    IReadOnlyCollection<IIntegrationEvent> IntegrationEvents { get; }
    void RaiseIntegrationEvent(IIntegrationEvent e);
    void ClearIntegrationEvents();
}
```

### End-to-end flow

```
Order.Create(…)
  └─ RaiseIntegrationEvent(new OrderCreated())          // intrinsic to the domain op — unforgettable

WithTransactionAsync → SaveChanges
  └─ IntegrationEventOutboxInterceptor.SavedChangesAsync (post id-assignment)
       for each tracked IHasIntegrationEvents entity with pending events:
         for each event:
           if event is IntegrationEvent && AggregateId == 0:
               stamp AggregateId = entity.Id             // solves the generated-id timing
           for each registered handler of the event type:    // fan out: one row per handler
             write outbox row:
               HandlerCode = "IntegrationEvent"                // single framework consumer
               Payload     = { Code, Data, TargetHandlerCode = handler.Code }
         entity.ClearIntegrationEvents()
       (AsyncLocal-guarded second SaveChanges flushes the rows inside the same transaction)
   ... rows commit atomically with the Order; roll back with it ...

OutboxDispatcherJob (recurring, after commit)
  └─ IntegrationEventOutboxHandler.HandleAsync(payload)   // one call per (event × handler) row
       p       = deserialize IntegrationEventOutboxPayload
       event   = registry.Rebuild(p.Code, p.Data)
       handler = _handlers.Single(h => h.EventType == event.GetType() && h.Code == p.TargetHandlerCode)
       await handler.HandleAsync(event, ct)               // throw → THIS row retries (this handler only)
```

Adding a 4th reaction = write one `IIntegrationEventHandler<OrderCreated>` + one DI registration. `Order`, the interceptor, and every other handler are untouched. That is the 1→N + open/closed fix.

### Why fan out at harvest (one row per handler)

One row per handler inherits the existing per-row retry — exactly how `Notifier` stages one row per channel — so a single handler's failure retries only its own row, never its siblings. Harvest learns the handler codes from a **lazily-built event-type→handler-code map** (built once on first harvest from the registered handlers; each handler exposes a stable `Code`, defaulting to its type name). That's a narrow, deliberate relaxation of "harvest knows nothing of delivery."

It must fan out **at harvest, not at dispatch.** The sweep runs each handler in its own DI scope and only ever persists the *row's* own state on the dispatcher's context; a dispatch-time fan-out would stage its child rows on the handler's scoped context, which the dispatcher never saves — they'd be lost. Harvest is the only place that writes the N rows atomically with the entity. Handlers should still be idempotent for the crash-replay window between a successful side effect and the row being marked dispatched (which replays that one handler's row).

### The generated-id timing problem

A store-generated `Order.Id` is unknown until `SaveChanges` runs, but the event is raised in `Order.Create()` *before* the id exists. Resolution: the harvester runs in the interceptor's **`SavedChangesAsync`** (ids assigned), reads the raising entity's `Id`, and stamps it onto `IntegrationEvent.AggregateId` — so a handler always receives the id without the domain code threading it in. Events needing more than the id carry their own fields, set at raise time from data already known pre-save.

Because the outbox rows are added *after* the entity write, they require a second persistence within the same transaction. The interceptor appends the rows and issues a second `SaveChangesAsync` (inside the surrounding `WithTransactionAsync`), guarded by a static `AsyncLocal<bool>` flag so that second save — which re-enters `SavedChangesAsync` — doesn't re-harvest. (`AsyncLocal`, not an instance field, because the interceptor is a singleton shared across requests/threads; the flag must flow with the in-progress save.)

## Design decisions

1. **Retry granularity → one row per handler (implemented).** Harvest fans out one outbox row per registered handler (each tagged with the handler's `Code`), so a handler's failure retries only its own row — matching `Notifier`'s per-channel rows. Harvest learns the codes from a lazily-built event-type→handler-code map. It must fan out at harvest, not dispatch: the sweep isolates each handler in its own scope and only persists the row's state, so dispatch-time child rows would never be saved. See [Why fan out at harvest](#why-fan-out-at-harvest-one-row-per-handler).
2. **Raise mechanism → aggregate-raised (implemented); `IIntegrationEventPublisher` escape hatch deferred.** Raising on the entity makes the fact intrinsic to the domain operation (unforgettable) and gives the harvester the `AggregateId` for free. The publisher (for events with no single tracked aggregate) is not built in v1 — add it when a non-aggregate event appears; it must write to the **current** scoped `DbContext` to keep the transactional guarantee.
3. **Relationship to notifications → siblings now, unify later (decided).** Do **not** refactor `INotification` into this in the first pass. Conceptually a channel is a handler whose `HandleAsync` renders + ships; collapsing `NotificationOutboxHandler` into `IntegrationEventOutboxHandler` is a clean later step once the event bus is proven. Keeping the shapes parallel (same registry/payload/per-target-row design) makes that merge cheap and non-breaking.
4. **Recipient-kind fix folds in here.** The notification recipient model's `FirstOrDefault()` resolver fails because a bare `long` carries no kind. Typed handlers remove the problem for the event tier outright (the handler loads whatever it needs by the typed event), and when notifications later become event handlers, a "recipient" is just data on the typed event — no opaque-id resolver to disambiguate. Design the two together.

## Naming rationale

The chosen term is **`IIntegrationEvent`**, not `IDomainEvent`. The traits we are building — *durable, transactional-outbox, dispatched after commit, fanned out to handlers* — are the textbook definition of an **integration event** (eShopOnContainers, the canonical .NET reference). `IDomainEvent` conventionally denotes the **synchronous, in-transaction, in-process** reaction — i.e. the *atomic tier we are deliberately not building*. Naming the durable thing `IIntegrationEvent` is both correct **and** reserves `IDomainEvent` for that future synchronous tier, so the two coexist with non-overlapping meaning.

The `…Code` attribute, `…TypeRegistry`, `…OutboxPayload`, and `…OutboxHandler` names are chosen to read as exact siblings of their `Notification…` counterparts, so a reader who knows the notification framework already knows this one.

## Out of scope

- The **synchronous in-process domain-event tier** (`IDomainEvent`) — atomic reactions stay inline. Reserved, not built.
- **Cross-process / broker publishing** — handlers run in-process; publishing an event onto an external bus is just *another handler*, not a framework concern here.
- **Ordering guarantees across handlers** — handlers are independent and unordered by design (the whole point). A reaction that must run before another belongs in one handler, or in the atomic tier.

## Slices / rollout

1. **Framework core — DONE** (`Spiderly.Shared/IntegrationEvents/` + `Interfaces/`): `IIntegrationEvent`, `IntegrationEvent`, `[IntegrationEventCode]`, `IIntegrationEventHandler` (+ `IntegrationEventHandler<T>`), `IHasIntegrationEvents`, `IntegrationEventTypeRegistry`, `IntegrationEventOutboxPayload`, `IntegrationEventOutboxHandler`, `IntegrationEventOutboxInterceptor<TOutbox>`, `AddIntegrationEvents()`, plus the `SpiderlyAddDbContext` interceptor wiring. 7 unit tests (`IntegrationEventTests`) cover registry round-trip + dup-code guard and dispatch (match by `EventType`, no-handler no-op, sibling-runs-then-throws-so-row-retries). `Spiderly.Shared` builds clean. *(Not built: `IntegrationEventHandlerRegistry`, `IIntegrationEventPublisher` — see [Design decisions](#design-decisions).)*
2. **Entity base — DONE** (folded into slice 1): `IHasIntegrationEvents` on `BusinessObject<T>` with a `[NotMapped]` lazy collection. No init-template change needed — the bus is opt-in (`AddIntegrationEvents()` is not in the default `Startup`) and the entity-base change ships in the package, not the template. Confirmed the source generator **hardcodes** the `BusinessObject<T>` base props (`ClassAnalyzer.GetPropertiesForBaseClasses`), so the new members are invisible to DTO/mapper generation.
3. **First consumer (PACMS) — DONE**: `OrderCreated` (`[IntegrationEventCode("OrderCreated")]`) + `RevalidateStorefrontOnOrderCreated` handler in `PACMS.Business/Services/IntegrationEvents/`; raised on the `Order` aggregate in `OrderIntake` (all payment methods, before `SaveChanges`); `AddIntegrationEvents(typeof(OrderCreated))` in `Startup` + the handler registered in `AppServiceExtensions`. PACMS.Business + PACMS.WebAPI build clean. PACMS was already on local Spiderly source.

   **Spec revision — migrated the revalidation reaction, NOT the confirmation email.** The confirmation email is the *wrong* first consumer: it's a **notification** (already correctly served by the notification outbox + renderer), and its timing is **payment-method-dependent** — COD fires it at creation (`OrderIntake`), Card fires it at payment approval (`OrderLifecycle.ApprovePaymentAsync`) — so it does not map to a single `OrderCreated` fact. `RevalidateAllProducts()` does map: it fired unconditionally for every order, post-commit, and was sitting **inside** `WithTransactionAsync` (an HTTP-triggering call inside the DB transaction). Moving it to an `OrderCreated` handler makes it post-commit, off the transaction and request path, durable, and retried by its outbox row. **`OrderOutboxStaging` is therefore unchanged** — it still owns the order notifications (confirmation / payment-failed / status-change) and the Wings export hand-off; it is neither shrunk nor retired by this slice. Accepted trade-off: revalidation now lands by the next outbox sweep (≤ ~1 min) instead of inline — fine for storefront stock-display freshness, and strictly safer (durable + out of the transaction).
4. **Unify the shared core with notifications — designed, ready to build:** see `docs/outbox-core-unification.md`. That design **rejects** the "notifications become event handlers" collapse this slice originally imagined, in favor of a shared *low-level* core (`[OutboxCode]` + `CodeTypeRegistry<TMarker>` + `OutboxEnvelope`) with channel and handler kept as distinct subscriber kinds; it also brings `IIntegrationEventPublisher` into scope. Still deferred there: per-handler retry rows, FireNow-for-events, and the recipient-kind fix.

## Open questions

- ~~**Interceptor second-save**~~ — **resolved**: append rows in `SavedChangesAsync`, then an `AsyncLocal<bool>`-guarded second `SaveChangesAsync` inside the existing `WithTransactionAsync`. Both the async and sync `SavedChanges` paths are handled.
- ~~**Event-type discovery**~~ — **resolved**: `AddIntegrationEvents(params Type[] markers)` discovers `[IntegrationEventCode]` events from the markers' assemblies, defaulting to the outbox entity's assembly. (The *handler*-registry discovery question only returns with v2 per-handler rows.)
- **Outbox row volume** — one row *per event* in v1 (not per handler), so volume tracks event count, not fan-out. Re-evaluate if v2 per-handler rows land on a high-fan-out event on a hot path.
