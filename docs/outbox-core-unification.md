# Outbox delivery core — unifying notifications & integration events — Design

> Status: **PROPOSED — design grilled & locked, ready to build.** Extracts the shared low-level machinery behind Spiderly's two near-duplicate "typed fact → outbox → subscribers" frameworks (notifications + integration events) into one core, while keeping the two specializations distinct. This is the refactor `docs/integration-events.md` slice 4 anticipated — but it **rejects** that slice's "notifications become event handlers" collapse in favor of a shared *low-level* core with two distinct subscriber kinds (see [Shape](#the-shape-shared-low-level-core-not-a-collapse)).
>
> Reached by a full design grill. The [decision log](#decision-log) is the binding summary; the rest is rationale + build plan. Pre-production, so **no back-compat** — the outbox-row JSON shape changes freely.

## Problem

Spiderly has two frameworks that are each *"a strongly-typed fact, given a stable code, serialized to an outbox row, rebuilt at delivery, dispatched to subscribers"*:

- **Notifications** (`INotification` → channels) — in production (order emails, ops alerts).
- **Integration events** (`IIntegrationEvent` → handlers) — `docs/integration-events.md`, slices 1–3 built.

They duplicate ~80% of the low-level plumbing:

| Integration events | Notifications | |
|---|---|---|
| `IntegrationEventTypeRegistry` | `NotificationTypeRegistry` | near-identical |
| `[IntegrationEventCode]` | `[NotificationCode]` | identical shape |
| `IntegrationEventOutboxHandler` | `NotificationOutboxHandler` | same rebuild + dispatch skeleton |
| `IntegrationEventOutboxPayload` | `NotificationOutboxPayload` | same `{Code, Data}` core |

Cloning a pattern instead of extracting its core is the design debt this fixes.

## The shape: shared *low-level* core, not a collapse

Two candidate shapes were weighed:

- **B (rejected) — collapse:** make a channel a kind of handler; notifications disappear into events. Rejected because a channel carries real message-delivery machinery a handler neither has nor wants (config-driven routing to N channels, per-channel recipient-address resolution, `IsConfigured`, dedupe/rate-limit, the FireNow mode). Collapsing either bleeds that into the generic handler model or throws away the channel richness order emails rely on.
- **A (chosen) — shared low-level core + two specializations:** one engine for `[OutboxCode]` + code↔type registry + `OutboxEnvelope` + serialize/rebuild. On top, **channel** (notifications) and **handler** (events) stay distinct interfaces. We DRY the plumbing without merging two genuinely different concerns (delivering a *message* vs. running *code*).

## The shared core

Three pieces, plus two tiny helpers. **Producers read the code off the type; only delivery touches the registry** (the eShop/MassTransit split — a producer never needs a registry, so publishing never couples to a subscriber existing).

```csharp
// ONE attribute, on both notification types and event types.
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OutboxCodeAttribute(string code) : Attribute { public string Code { get; } = code; }

// Producer-side: read the stable code straight off the runtime type. No registry.
public static class OutboxCode
{
    public static string Of(Type type) => type.GetCustomAttribute<OutboxCodeAttribute>()?.Code
        ?? throw new InvalidOperationException($"{type.Name} is missing [OutboxCode].");
}

// The serialized row body. Events use it as-is; notifications extend it.
public class OutboxEnvelope
{
    public string Code { get; set; }
    public string Data { get; set; }
    public static OutboxEnvelope For(object fact) => new()
    {
        Code = OutboxCode.Of(fact.GetType()),
        Data = JsonSerializer.Serialize(fact, fact.GetType()),
    };
}

// Delivery-side: code↔type, built from an EXPLICIT type list (no assembly scan), dup-code guarded.
// One generic class, instantiated once per marker (TMarker = INotification, then IIntegrationEvent).
public sealed class CodeTypeRegistry<TMarker>
{
    private readonly Dictionary<string, Type> _byCode = new();
    public CodeTypeRegistry(IEnumerable<Type> types)
    {
        foreach (Type t in types)
        {
            string code = OutboxCode.Of(t);
            if (!_byCode.TryAdd(code, t))
                throw new InvalidOperationException(
                    $"Duplicate [OutboxCode(\"{code}\")] on {_byCode[code].Name} and {t.Name} (both {typeof(TMarker).Name}).");
        }
    }
    public Type ResolveType(string code) => _byCode.TryGetValue(code, out Type t) ? t
        : throw new InvalidOperationException($"No {typeof(TMarker).Name} registered for code '{code}'.");
    public TMarker Rebuild(string code, string data) => (TMarker)JsonSerializer.Deserialize(data, ResolveType(code))
        ?? throw new InvalidOperationException($"Outbox fact '{code}' deserialized to null.");
}
```

**Deleted by this:** `NotificationTypeRegistry`, `IntegrationEventTypeRegistry`, `[NotificationCode]`, `[IntegrationEventCode]`, `IntegrationEventOutboxPayload`, and all assembly-scanning (`*.Discover(assemblies)`).

## Specialization 1 — events (deltas only)

```csharp
[OutboxCode("OrderCreated")]                         // was [IntegrationEventCode]
public class OrderCreated : IntegrationEvent { }       // IntegrationEvent (AggregateId) unchanged

// Harvest (interceptor) — reads [OutboxCode] off the type directly, no registry → fully decoupled from handlers.
context.Add(new TOutbox {
    HandlerCode = IntegrationEventOutboxHandler.HandlerCode,
    Payload = JsonSerializer.Serialize(OutboxEnvelope.For(integrationEvent)),  // {Code, Data}
});

// Explicit publisher — same envelope, for facts with no aggregate write (webhooks/jobs/security).
public class IntegrationEventPublisher(IOutbox outbox) : IIntegrationEventPublisher
{
    public void Publish(IIntegrationEvent e) =>
        outbox.Enqueue(IntegrationEventOutboxHandler.HandlerCode, OutboxEnvelope.For(e));
}

// Delivery — registry rebuild + dispatch-by-EventType (unchanged dispatch).
public async Task HandleAsync(string payload, CancellationToken ct)
{
    OutboxEnvelope env = JsonSerializer.Deserialize<OutboxEnvelope>(payload);
    IIntegrationEvent ev = _registry.Rebuild(env.Code, env.Data);     // CodeTypeRegistry<IIntegrationEvent>
    foreach (var h in _handlers.Where(h => h.EventType == ev.GetType()))
        await h.HandleAsync(ev, ct);
}
```

## Specialization 2 — notifications (deltas only)

```csharp
[OutboxCode("OrderConfirmed")]                        // was [NotificationCode]
public class OrderConfirmedNotification : INotification { /* ... */ }

public sealed class NotificationOutboxPayload : OutboxEnvelope   // extends the shared envelope
{
    public long? RecipientId { get; set; }
    public string ChannelCode { get; set; }
}

// Notifier (producer) reads the code off the type; one row per channel (unchanged fan-out).
string code = OutboxCode.Of(notification.GetType());

// NotificationDeliveryExecutor (delivery) — same shared rebuild; channel + recipient dispatch unchanged.
INotification notification = _registry.Rebuild(env.Code, env.Data); // CodeTypeRegistry<INotification>
// → find channel by ChannelCode → IsConfigured → resolve recipient → channel.SendAsync
```

## Registry wiring — explicit, no scanning

Both registries are built from **what's already explicitly declared**, just different surfaces:

```csharp
// Notifications — the route keys ARE the deliverable set (Notifier drops unrouted before serialize):
services.AddSingleton(new CodeTypeRegistry<INotification>(routingMap.Routes.Keys));

// Events — the explicitly-registered event TYPES (typeof, no longer an assembly handle):
spiderly.AddIntegrationEvents(typeof(OrderCreated), typeof(OrderShipped));
services.AddSingleton(new CodeTypeRegistry<IIntegrationEvent>(eventTypes));
```

Why types, not handlers, for events: harvest reads `[OutboxCode]` to *produce* a row, **before** any handler is consulted (handlers resolve at delivery). A handler-derived registry would make "can I save an order that raises an event" depend on "does a reaction exist" → deleting a handler breaks the publisher. Type-derived keeps publish decoupled from subscribe, and preserves the *unknown-code = loud bug* vs *no-handler = quiet no-op* distinction.

## Enqueue model — two paths, one delivery

Kept distinct (the industry standard: domain-events-harvested vs integration-events-pushed, bridged explicitly — eShop/Grzybek/MassTransit). The shared core contributes only `OutboxEnvelope.For` (serialize) + `IOutbox.Enqueue` (stage).

```
raise on aggregate (interceptor harvest) ─┐
explicit IIntegrationEventPublisher.Publish ┼─► identical OutboxEnvelope row ─► one outbox handler ─► subscribers
INotifier.Notify (per-channel rows)        ─┘
```

`IIntegrationEventPublisher` is **in scope** — it covers facts with no aggregate write (payment webhook, security event, scheduled job). Both event triggers produce an identical envelope → same `IntegrationEventOutboxHandler` → handlers don't know which trigger was used.

## Delivery mode (FireNow vs Outbox)

`FireNow` (immediate Hangfire enqueue) vs `Outbox` (transactional row) is a property of the **trigger context** (is the caller in a transaction?), **not** the fact kind:

- **Harvest** (aggregate events) → always Outbox (transactional by construction).
- **Explicit push** (`Notify` or `Publish`) → can be either.

v1 wires `FireNow` for **notifications only** (the existing ops-alert consumers). Events are **Outbox-only by YAGNI, not by law** — `Publish(evt, FireNow)` is a trivial later addition (extract an `IntegrationEventDeliveryExecutor` mirroring `NotificationDeliveryExecutor` + a Hangfire job). The design must not enshrine "events are Outbox-only."

## Naming

| Final | Replaces | Why |
|---|---|---|
| `[OutboxCode("…")]` | `[NotificationCode]` + `[IntegrationEventCode]` | role not kind; reads on both |
| `CodeTypeRegistry<TMarker>` | `NotificationTypeRegistry` + `IntegrationEventTypeRegistry` | says exactly what it is |
| `OutboxEnvelope { Code, Data }` | `IntegrationEventOutboxPayload`; base of `NotificationOutboxPayload` | "envelope" = type-id + body (MassTransit/NServiceBus vocab) |

Kept as-is: `INotification`, `IIntegrationEvent`, `INotificationChannel`, `IIntegrationEventHandler`, `IntegrationEvent`, `IIntegrationEventPublisher`, both outbox handlers, `NotificationDeliveryExecutor`.

## Decision log (binding)

1. **Shape A** — shared low-level core, two specializations; channel and handler stay distinct (reject collapse).
2. **Two outbox handlers** sharing the rebuild — not one; `HandlerCode` is the dispatch selector.
3. **One `[OutboxCode]` + one generic `CodeTypeRegistry<TMarker>`** (two instances), no shared base marker, per-kind codes.
4. **Kill assembly scanning.** Notifications ← `Routes.Keys`; events ← explicit `typeof(...)`. Producers read `[OutboxCode]` directly. The event delivery `CodeTypeRegistry` stays delivery-only, but **harvest also consults a lazily-built event-type→handler-code map** to fan out one row per handler (see per-handler retry rows below) — a deliberate, narrow relaxation of the original "harvest knows nothing of delivery."
5. **Two enqueue paths kept** (inherent); shared core = serialize + `IOutbox.Enqueue`.
6. **`IIntegrationEventPublisher` in scope** for non-aggregate facts; both triggers → identical envelope → one delivery path.
7. **FireNow = trigger-context, not fact-kind.** Harvest always Outbox; FireNow wired for notifications only in v1; events Outbox-only by YAGNI, not law.
8. **No back-compat.** Breaking the outbox JSON field names is fine (pre-production; both repos allow breaking changes).

**Done (added after the initial unification):** per-handler retry rows — harvest fans out one outbox row per registered `IIntegrationEventHandler` (each addressed by the handler's `Code`), so a single handler's failure retries only its own row, matching the notification per-channel model. `IntegrationEventOutboxHandler` now delivers one handler per row (no more run-all-then-`AggregateException`). Why harvest, not dispatch: the sweep runs each handler in an isolated DI scope and only persists the row's own state, so a dispatch-time fan-out's child rows (staged on the handler's scoped context) would never be saved — harvest is the only place that writes the N rows atomically with the entity.

**Deferred (out of scope):** raise-harvest for notifications; FireNow-for-events; the notification recipient-kind fix (separate filed issue).

## Build plan (slices)

1. **Shared core** — `[OutboxCode]`, `OutboxCode.Of`, `OutboxEnvelope`, `CodeTypeRegistry<TMarker>` (new files; e.g. `Spiderly.Shared/Outbox/`). Unit tests for the registry (dup-guard, ResolveType, Rebuild round-trip).
2. **Rewire integration events** onto the core: `[OutboxCode]`, `CodeTypeRegistry<IIntegrationEvent>`, harvest + publisher use `OutboxEnvelope.For`, `IntegrationEventOutboxHandler` uses `Rebuild`; add `IIntegrationEventPublisher`; `AddIntegrationEvents(typeof(...))` registers types directly. Update the 11 existing integration-event tests for renames.
3. **Rewire notifications** onto the core: `[OutboxCode]`, `CodeTypeRegistry<INotification>` from `Routes.Keys`, `NotificationOutboxPayload : OutboxEnvelope`, `Notifier`/`NotificationDeliveryExecutor` use `OutboxCode.Of` + `Rebuild`. Update notification tests.
4. **Delete** the duplicated classes + all `Discover(assemblies)` paths.
5. **Reverify PACMS** — rename `[IntegrationEventCode]`→`[OutboxCode]` on `OrderCreated`, `AddIntegrationEvents(typeof(OrderCreated))` unchanged, notification types `[NotificationCode]`→`[OutboxCode]`; build PACMS.Business + PACMS.WebAPI.
6. **Housekeeping** — audit the `spiderly init` template (`NetAndAngularFilesGenerator.cs`) for renamed APIs; regenerate framework-metadata SSOT + agent bundle if the renamed attributes are covered; update any consumer docs/skills referencing the old names.

Each slice builds + tests green before the next.

## Open questions

- **One file/namespace for the core** — `Spiderly.Shared/Outbox/` (next to `IOutbox`) vs a new `Spiderly.Shared/Delivery/`. Lean `Outbox/` (it's the outbox's typed layer).
- **`OutboxEnvelope` inheritance vs composition** — notifications extend it (`: OutboxEnvelope`) vs hold one. Inheritance is fewer types; revisit only if a third specialization appears.
