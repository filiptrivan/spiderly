# Spiderly Notification & Outbox Framework — Design

> Status: **COMPLETE — slices 1 + 2 + 3 + 4 implemented.** Slice 3 (Telegram removal + ops unification): ops notifications (`UnhandledException`/`SecurityEvent`/`JobFailed`) + auto-routing to Email; all PACMS app callers + the 3 framework-internal callers (`SpiderlyExceptionHandler`, rate-limiter rejection, `HangfireFailedJobNotificationFilter`) migrated to `INotifier`; `INotificationDispatcher`/`HangfireNotificationDispatcher`/old jobs/`TelegramNotifier` deleted; Telegram removed from `NotificationOptions`/appsettings/schema/docker-compose/deploy-workflow/website docs. Verified: `Spiderly.Shared` tests 20/20, `PACMS.Business` + `PACMS.WebAPI` build clean. **Slice 4 in progress (option 4c):** step 1 (framework `IEmailRenderer` seam — content from a DB-capable renderer, falling back to self-contained `ToEmail`) ✅; step 2 (PACMS reshaped onto the framework outbox — `HandlerCode` entity + migration, `OrderOutboxStaging` as `IOutbox` facade, 4 `IOutboxHandler`s, bespoke dispatcher/enum deleted) ✅. Step 3 ✅: the 3 order emails are now thin `Order*Notification`s (Outbox delivery) + async `Order*EmailRenderer`s (load fresh, reuse `OrderEmailBuilder`) routed via `INotifier` to Email, with an order-backed/guest-safe `OrderNotificationRecipientResolver`; the `IEmailRenderer` seam was made async; `CustomerNotifier`'s 3 order send-methods removed (it now only does loyalty/cart/stock). Verified: framework tests 23/23, PACMS `OutboxTests` + `OrderServiceCartLifecycleTests` 7/7, `PACMS.Business` + `PACMS.WebAPI` build clean.
> This doc is the shared design agreed during the grilling session. Keep it in sync as slices land.
>
> **Update (2026-07-09, ops notifications reversed):** slice 3's "ops unification" was a category error and has been deleted — `UnhandledExceptionNotification`/`SecurityEventNotification`/`JobFailedNotification`, `IExceptionReporter`/`NotificationExceptionReporter`, `HangfireFailedJobNotificationFilter`, and the `AddNotifications` default routes are gone. Operational telemetry (errors, security events, failed jobs) is emitted as structured Error/Warning logs + exception diagnostics only; alerting is the app's error tracker's job (tracker integration tracked in the "error-tracker integration" GitHub issue). The notification framework is business notifications only (order shipped, admin workflow items); everything below about ops notifications is historical.
>
> **Update (outbox-core unification):** the code↔type registry and the `[NotificationCode]` attribute referenced below were extracted into the shared outbox core — now `CodeTypeRegistry<INotification>` (built from the route keys, no assembly scan) and `[OutboxCode]`, with `NotificationOutboxPayload : OutboxEnvelope`. Notification *behavior* (router → channels → recipient → FireNow/Outbox) is unchanged. See `docs/outbox-core-unification.md`.

## Problem

A Spiderly app needs to send operational and user-facing messages through many destinations — email, Telegram, GitHub issues, a coding agent, Discord, customer SMS, an in-app bell, etc. Spiderly **cannot** build an integration for every destination, but it must make **adding one cheap** and must let consumers add destinations the framework has never heard of.

Today Spiderly has `INotificationDispatcher` + `HangfireNotificationDispatcher` + `UnhandledExceptionNotificationJob`, but the **channels are hard-coded** into the job classes (email + Telegram baked in via constructor). A consumer can't add GitHub/agent/Discord without editing framework code. This framework inverts that.

## Two axes

The system separates two independent axes:

- **Notification** — *what happened* and *what it says* (e.g. `OrderShippedNotification`).
- **Channel** — *where it goes* / the transport (Email, Telegram, …).

A durable **transactional outbox** is the substrate underneath both.

## Core decisions (and the rationale)

1. **Audience: ops *and* customers.** Dynamic recipients are in scope, so this is a general notification framework (think Laravel Notifications), not just an ops-alert helper.

2. **Delivery via Hangfire** (durable, auto-retried). We deliberately did **not** add a post-commit hook to `WithTransactionAsync` (too invasive to a critical primitive). Therefore **two levels only**:
   - `FireNow` — enqueue to Hangfire immediately; loss-tolerant (ops pings, back-in-stock).
   - `Outbox` — row written *inside* the transaction, swept after commit; guaranteed + crash-durable (orders, money, tokens).
   - The middle "fire only if the save committed, but no DB row" case is handled by the programmer simply calling `Send` *after* `WithTransactionAsync` returns — no framework feature.

3. **Channels are pluggable via DI, no framework enum.** `INotificationChannel` discovered as `IEnumerable<INotificationChannel>`. Adding a channel = one class + register. **Core ships Email only**; Telegram/GitHub/agent/Discord are consumer-written (Telegram extracted from core into an optional package). An enum can't live in the framework because consumers must extend it.

4. **Content via capability interfaces** (NOT a generic `Title/Body` bag, NOT a Template-Method base class):
   - Each **channel** ships its own content interface, e.g. `ITelegramNotification { string ToTelegram(); }`.
   - A **notification** implements only the channels it supports.
   - At send time the framework hands the notification object to each channel; the channel does `if (n is IXxxNotification x) …`.
   - **Why not Template Method / per-channel base methods:** the base class would have to declare `ToDiscord()` etc., but the base class lives in the framework, which can't know about Discord. Capability interfaces move ownership of the contract to the channel (which may be third-party) — the only shape that supports channels the framework never heard of. Also: single-inheritance is preserved, contracts stay segregated (ISP), and a notification only depends on channels it uses.
   - **Fallback when a notification doesn't implement a channel's interface: send nothing** (explicit; a notification only goes where it opted in). No mandatory common text interface.

5. **Recipients.** The recipient supplies its **own per-channel address** (a `User` returns its email for Email, its phone for SMS, …) — the framework never learns the consumer's user schema. Two entry points:
   - `NotifyAdmins(notification)` — static config recipients (ops).
   - `Notify(recipient, notification)` — dynamic recipient.

6. **Routing** (which channels a notification uses): **code-first** via an `INotificationRouter`; swappable for a DB/admin-backed implementation if the consumer wants runtime control. Effective channels = *notification implements the channel's interface* ∩ *router enables it* ∩ *channel `IsConfigured`*.

7. **Level is declared on the notification** (`FireNow`/`Outbox`), not passed per call — so the dangerous ones (order/money) are locked to `Outbox` in one place and can't be sent the unsafe way by mistake.

8. **Dedupe is opt-in** via a `DedupeKey` on the notification (null = no dedupe). Ops notifications opt in (collapse an exception storm to one alert); customer notifications leave it off (different recipients must never be merged). Reuses the existing `NotificationRateLimiter`.

## The outbox (substrate)

A **general** transactional outbox — not notification-specific. PACMS proved this: its `OutboxMessage` already carries both notifications (`OrderConfirmation`) and non-notification work (`WingsExportRequested`, a job hand-off).

- Row is **dumb**: `HandlerCode` (string) + opaque `Payload` (JSON) + dispatch/retry bookkeeping. **No recipient / notification fields** — those leak the notification concern into a general primitive.
- `IOutboxHandler` registry replaces a hard-coded dispatch switch. Open-extensible: add a handler + register it, no framework change.
- **Notifications are ONE handler** (`NotificationOutboxHandler`): its payload is `{ NotificationType, NotificationData, RecipientId }`. The recipient lives *inside the notification handler's payload*, never on the general row.
- Generic over the consumer's concrete entity (`OutboxDispatcherJob<TOutbox> where TOutbox : IOutboxMessage`), mirroring `ApplicationDbContext<TUser>`. The concrete `OutboxMessage` entity is **scaffolded into the consumer** by `spiderly init` (same path as `User`/`Role`/`Permission`), which gives it a generated admin page + migration with no new generator machinery.

### Reconstructing a notification from the outbox
Notifications are **self-contained data objects** (carry all data their render methods need). The outbox stores the type code + JSON. At sweep time: deserialize → for each routed channel, render from the notification (channels call the capability method). "Frozen at event time" is the correct behavior for a notification.

## Build order (slices)

1. **General outbox** — `IOutboxMessage` / `IOutboxHandler` / `IOutbox` / `Outbox<T>` / `OutboxDispatcherJob<T>` + DI + recurring sweep + scaffolded entity/service/controller + tests. ✅ **DONE.**
2. **Notification framework** — `INotification` (+ `Delivery` level, optional `DedupeKey`), `INotificationChannel` (+ `Code`), capability interfaces (`IEmailNotification`/`IEmailRecipient`), `INotificationRouter` + fluent `AddNotifications` routing, `INotifier` (`Notify`/`NotifyAdmins`), the built-in `EmailChannel`, `[NotificationCode]` + `NotificationTypeRegistry`, `INotificationRecipientResolver`, and `NotificationOutboxHandler` (the outbox consumer for `Outbox`-level notifications). ✅ **DONE.**
3. **Rework the existing ops path** — rebuild `UnhandledExceptionNotificationJob` / security events around `INotificationChannel`; keep Email built-in, extract `TelegramNotifier` into an optional channel/package.
4. **Migrate PACMS** off its bespoke `OutboxMessage` onto the framework outbox; move its order/Wings handlers to `IOutboxHandler`s.

## Known follow-ups
- Angular admin **row-action buttons** (Retry/Dismiss) for the outbox table — endpoints exist; wiring buttons into the generated table is a frontend-generation task.
- End-to-end `spiderly init` generate-and-build verification of the scaffolded outbox files (CI e2e job; the checked-in `tests/e2e-fixtures/backend/` may need regenerating).
