# The job seam: Hangfire jobs come only from the container

> Status: **IMPLEMENTED**, opt-in. Code: `Spiderly.Shared/Jobs/` (`ISpiderlyJob`, `SpiderlyJobActivator`,
> `SpiderlyJobSchedulingExtensions`, `SpiderlyJobRegistration`) and `SpiderlyBuilder.AddJobs`.
> Tests: `Spiderly.Shared.Tests/HangfireJobActivationTests`, `SpiderlyJobActivatorTests`, `JobRegistrationTests`,
> `SpiderlyJobSchedulingTests`, `SpiderlyScheduledJobsTests`.

## Problem

An ASP.NET Core host can validate its service graph when it builds (`ValidateOnBuild`, `ValidateScopes`): every
registered service's constructor is checked against the registrations, so a missing dependency stops the boot instead of
failing the first request or the first job that needs it. That check only reaches what is registered.

Hangfire jobs usually are not. Hangfire.NetCore's default activator (`AspNetCoreJobActivatorScope.Resolve`, read in the
1.8.23 source) builds a job with `ActivatorUtilities.GetServiceOrCreateInstance`: a registered job comes from the
container, and an unregistered one is constructed on the spot from whatever the container holds. The app runs either
way, so nothing says the job was never registered, and its constructor is first judged when the job runs, often at
night, in production. Spiderly had the same gap in its own code: `SpiderlyUseOutboxRecurringJob` scheduled
`OutboxRetentionJob<T>` and `OutboxHealthJob<T>`, while `AddOutbox<T>()` registered only `OutboxDispatcherJob<T>`.

## The decision

Opting in has one call, and after it a job the container does not know is never built:

```csharp
services.AddSpiderly<MyDbContext>(Configuration, spiderly =>
{
    // ...
    spiderly.AddJobs(typeof(Startup).Assembly, typeof(InvoiceExportJob).Assembly);
});
```

| Piece | What it does |
| --- | --- |
| `ISpiderlyJob` | A marker interface with no members. A class that implements it is a job. |
| `spiderly.AddJobs(assemblies)` | Registers every non-abstract class implementing `ISpiderlyJob` in those assemblies as a scoped service (`TryAdd`, so an app's own registration of a job wins), and makes `SpiderlyJobActivator` the container's `JobActivator`. One call, because the two are co-required: the activator without the scan refuses every job, and the scan without the activator leaves the gap open. |
| `SpiderlyJobActivator` | Opens one container scope per job, as Hangfire.NetCore's activator does, and resolves the job with `GetService`. A job type the container does not know throws, naming the type and the fix. |
| `EnqueueJob<TJob>`, `AddOrUpdateJob<TJob>` | Extension methods on `IBackgroundJobClient` and `IRecurringJobManager`, constrained to `TJob : ISpiderlyJob`, that forward to Hangfire's own generic methods. |

The marker and the scan make every job a registered service, so a host that validates its graph judges every job's
constructor at boot. The activator makes what the boot validated exactly what runs. The typed methods let an app make
an unmarked job a compile error: it bans Hangfire's unconstrained `Enqueue<T>` and `AddOrUpdate<T>` (with a
banned-symbols analyzer, for example) and schedules through these. Spiderly does not impose that ban; the policy is the
app's.

### Why the container's activator wins

Hangfire.NetCore's `AddHangfire` registers its activator with `TryAddSingleton<JobActivator>`, and
`AddHangfireServer` builds its server with `provider.GetService<JobActivator>()` (both read in the 1.8.23 source). So
whichever `JobActivator` registration the container holds is the one the server uses. `AddJobs` removes any earlier
registration and adds its own: when `AddHangfire` ran first, its registration is replaced; when it runs later, its
`TryAdd` finds ours and adds nothing. `HangfireJobActivationTests` runs Hangfire.NetCore's real server over in-memory
storage in both orders, and without the opt-in as the control.

`AddHangfire` also points the process-wide `JobActivator.Current` at Hangfire.NetCore's activator. The hosted server
does not read it, so `AddJobs` leaves it alone; a server an app builds by hand (`new BackgroundJobServer(options)`)
needs `options.Activator` set to the container's `JobActivator`. Because `AddJobs` replaces an activator registered
before it, an app that wants its own registers it after `AddSpiderly`.

### Spiderly's own jobs

Spiderly's four jobs (`OutboxDispatcherJob<T>`, `OutboxRetentionJob<T>`, `OutboxHealthJob<T>`,
`NotificationDeliveryJob`) carry the marker and are scheduled through the typed methods, so the compiler holds them to
it. Each is registered by the feature that schedules it (`AddOutbox<T>()`, `AddNotifications(...)`), whether or not the
app calls `AddJobs`, and the outbox jobs are registered closed over the app's outbox type, since a container cannot
validate an open generic registration at boot. `SpiderlyScheduledJobsTests` reads the jobs off what Spiderly actually
hands Hangfire and asks the container for each.

`SpiderlyUseOutboxRecurringJob` schedules through the container's `IRecurringJobManager` instead of Hangfire's static
`RecurringJob`, so it writes to the storage the container holds (an app that registers its own `JobStorage` gets its
recurring jobs there), and it needs `services.AddHangfire(...)`, which an app that schedules jobs already has.

## Paths not taken

- **Same names as Hangfire's (`Enqueue<TJob>`).** An extension method beside Hangfire's of the same name and parameters
  is an ambiguous call (CS0121) in any file that imports both namespaces, which is every file that schedules a job: a
  generic constraint does not break the tie. Hence `EnqueueJob` and `AddOrUpdateJob`.
- **A wrapper service** (`IJobScheduler`) instead of extension methods. It would replace `IBackgroundJobClient` in
  every consumer and every fake, and would not compose with an app's own `IRecurringJobManager` decorators.
- **An explicit list** (`AddJobs(typeof(InvoiceExportJob), ...)`), as `AddIntegrationEvents` and the notification
  routes declare their types without scanning. There the list is the registry, and a type missing from it fails at
  boot. Here a marked job left off a list would still compile through the typed methods and be refused only when it
  runs, the failure the seam exists to move to boot. The scan makes "carries the marker" and "is registered" one fact,
  so the compiler's check on the marker is a check on the registration.
- **An attribute marker** (`[SpiderlyJob]`), like Spiderly's generator attributes. A generic constraint can only name
  an interface or a base class, so an attribute could not make an unmarked job a compile error.
- **A container that refuses only unmarked types.** The activator refuses what is not registered, the property boot
  validation needs. An app may register a job some other way (a factory, another lifetime) and the activator builds it.
- **Finding job types by scanning the code for `Enqueue<T>` calls.** It detects after the fact what the compiler can
  refuse.

Not done here: the `spiderly init` template does not opt new apps in. A templated app's jobs run through the seam only
in CI's end-to-end job, so that is its own change, proven there.

## Open generic jobs

A marked open generic class (`ImportJob<TSource> : ISpiderlyJob`) is registered as an open generic, so the activator can
build any closed form. The container cannot validate an open generic registration at boot; register the closed forms
you use as well (as `AddOutbox<T>()` does) when you want them judged there.
