---
name: background-jobs
description: Write and schedule Hangfire background jobs in a Spiderly app so the container builds every job. Use when adding a job class, enqueueing a job, adding a recurring job, opting in with spiderly.AddJobs, or seeing "is not registered in the container, so it is not built".
---

# Background Jobs

Spiderly runs background work on Hangfire. Opt in to the job seam and every job is a registered service: a host that
validates its service graph at boot judges each job's constructor, and Hangfire builds jobs only from the container.

## Opt in

```csharp
services.AddSpiderly<MyDbContext>(Configuration, spiderly =>
{
    // ...
    spiderly.AddJobs(typeof(Startup).Assembly, typeof(InvoiceExportJob).Assembly);
});
```

`AddJobs` registers every non-abstract class implementing `ISpiderlyJob` in the given assemblies (scoped: one instance
per job run) and replaces Hangfire.NetCore's job activator with `SpiderlyJobActivator`. It works with
`services.AddHangfire(...)` before or after it. A second call adds assemblies.

## Write a job

```csharp
using Spiderly.Shared.Jobs;

public class InvoiceExportJob : ISpiderlyJob
{
    private readonly IApplicationDbContext _context;

    public InvoiceExportJob(IApplicationDbContext context) => _context = context;

    public async Task ExportAsync(long invoiceId) { /* ... */ }
}
```

- Implement `ISpiderlyJob`, take dependencies in the constructor, keep method arguments simple and serializable.
- No `services.AddScoped<InvoiceExportJob>()` needed: the scan registers it. A registration of your own (another
  lifetime, a factory) wins over the scan's.

## Schedule it

Use the typed methods, which accept only an `ISpiderlyJob`:

```csharp
// Injected IBackgroundJobClient
backgroundJobClient.EnqueueJob<InvoiceExportJob>(job => job.ExportAsync(invoiceId));

// IRecurringJobManager, in the Configure phase
IRecurringJobManager jobs = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();
jobs.AddOrUpdateJob<InvoiceExportJob>("invoice-export-nightly", job => job.ExportAllAsync(), "0 2 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

Do not schedule with Hangfire's `Enqueue<T>` / `AddOrUpdate<T>`: they accept any type, so an unmarked job compiles,
and the activator refuses it when it runs. An app can ban them with a banned-symbols analyzer to make that a compile
error.

## What fails where

| Mistake | Where it fails |
| --- | --- |
| Job class without `ISpiderlyJob` scheduled through the typed methods | Compile error |
| Job scheduled through Hangfire's own API, never registered | When it runs: `The Hangfire job '...' is not registered in the container, so it is not built.` |
| Job's constructor needs a service nobody registered | At boot when the host validates its graph (`ValidateOnBuild`), else when it runs |

## Spiderly's own jobs

`AddOutbox<TOutbox>()` registers the dispatcher, retention and health jobs that `app.SpiderlyUseOutboxRecurringJob<TOutbox>()`
schedules; `AddNotifications(...)` registers `NotificationDeliveryJob`. They are registered whether or not you call
`AddJobs`. `SpiderlyUseOutboxRecurringJob` schedules through the container's `IRecurringJobManager`, so it needs
`services.AddHangfire(...)`.

## Generic jobs

A marked open generic class (`ImportJob<TSource> : ISpiderlyJob`) is registered as an open generic. The container
cannot validate an open generic registration at boot, so also register each closed form you schedule
(`services.AddScoped<ImportJob<CsvSource>>()`) to have it judged there.

## Testing

A job is a plain class: construct it with its dependencies and call its method. To test what code schedules, pass a fake
`IBackgroundJobClient` / `IRecurringJobManager` that records the `Job` it is handed (`job.Type`, `job.Method`,
`job.Args`).
