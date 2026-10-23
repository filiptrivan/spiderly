using System.Linq.Expressions;
using Hangfire;

namespace Spiderly.Shared.Jobs
{
    /// <summary>
    /// Typed Hangfire scheduling that accepts only an <see cref="ISpiderlyJob"/>, so every job scheduled through it is
    /// one <c>spiderly.AddJobs(...)</c> registers. Each method forwards to Hangfire's own generic method of the same shape.
    /// An app that wants the compiler to refuse an unmarked job bans Hangfire's unconstrained generic
    /// <c>Enqueue&lt;T&gt;</c> / <c>AddOrUpdate&lt;T&gt;</c> (for example with a banned-symbols analyzer) and schedules
    /// through these.
    /// <para>The names differ from Hangfire's on purpose: an extension method named <c>Enqueue&lt;TJob&gt;</c> beside
    /// Hangfire's in a file that imports both namespaces is an ambiguous call, because a generic constraint does not
    /// break the tie between two otherwise identical candidates.</para>
    /// </summary>
    public static class SpiderlyJobSchedulingExtensions
    {
        /// <summary>
        /// Enqueues an async method of the marked job <typeparamref name="TJob"/> to run in the background.
        /// <example>
        /// <code>
        /// backgroundJobClient.EnqueueJob&lt;InvoiceExportJob&gt;(job =&gt; job.ExportAsync(invoiceId));
        /// </code>
        /// </example>
        /// </summary>
        /// <typeparam name="TJob">The job, built by the container when the job runs.</typeparam>
        /// <param name="client">The Hangfire client, usually injected as <see cref="IBackgroundJobClient"/>.</param>
        /// <param name="methodCall">The method to run and its arguments, serialized into the job.</param>
        /// <returns>The background job's id.</returns>
        public static string EnqueueJob<TJob>(this IBackgroundJobClient client, Expression<Func<TJob, Task>> methodCall)
            where TJob : ISpiderlyJob
            => client.Enqueue(methodCall);

        /// <summary>Enqueues a synchronous method of the marked job <typeparamref name="TJob"/> to run in the background.</summary>
        /// <typeparam name="TJob">The job, built by the container when the job runs.</typeparam>
        /// <param name="client">The Hangfire client, usually injected as <see cref="IBackgroundJobClient"/>.</param>
        /// <param name="methodCall">The method to run and its arguments, serialized into the job.</param>
        /// <returns>The background job's id.</returns>
        public static string EnqueueJob<TJob>(this IBackgroundJobClient client, Expression<Action<TJob>> methodCall)
            where TJob : ISpiderlyJob
            => client.Enqueue(methodCall);

        /// <summary>
        /// Adds or updates the recurring job <paramref name="recurringJobId"/>, which runs an async method of the marked
        /// job <typeparamref name="TJob"/> on <paramref name="cronExpression"/>.
        /// <example>
        /// <code>
        /// recurringJobManager.AddOrUpdateJob&lt;InvoiceExportJob&gt;("invoice-export-nightly", job =&gt; job.ExportAllAsync(), "0 2 * * *");
        /// </code>
        /// </example>
        /// </summary>
        /// <typeparam name="TJob">The job, built by the container each time it runs.</typeparam>
        /// <param name="manager">The recurring job manager, usually resolved as <see cref="IRecurringJobManager"/>.</param>
        /// <param name="recurringJobId">The recurring job's id; adding it again replaces its schedule.</param>
        /// <param name="methodCall">The method to run and its arguments, serialized into the job.</param>
        /// <param name="cronExpression">When it runs, e.g. <c>Cron.Daily()</c> or <c>"*/5 * * * *"</c>.</param>
        /// <param name="options">Time zone and queue; Hangfire's defaults (UTC) when omitted.</param>
        public static void AddOrUpdateJob<TJob>(
            this IRecurringJobManager manager,
            string recurringJobId,
            Expression<Func<TJob, Task>> methodCall,
            string cronExpression,
            RecurringJobOptions? options = null)
            where TJob : ISpiderlyJob
            => manager.AddOrUpdate(recurringJobId, methodCall, cronExpression, options ?? new RecurringJobOptions());

        /// <summary>
        /// Adds or updates the recurring job <paramref name="recurringJobId"/>, which runs a synchronous method of the
        /// marked job <typeparamref name="TJob"/> on <paramref name="cronExpression"/>.
        /// </summary>
        /// <typeparam name="TJob">The job, built by the container each time it runs.</typeparam>
        /// <param name="manager">The recurring job manager, usually resolved as <see cref="IRecurringJobManager"/>.</param>
        /// <param name="recurringJobId">The recurring job's id; adding it again replaces its schedule.</param>
        /// <param name="methodCall">The method to run and its arguments, serialized into the job.</param>
        /// <param name="cronExpression">When it runs, e.g. <c>Cron.Daily()</c> or <c>"*/5 * * * *"</c>.</param>
        /// <param name="options">Time zone and queue; Hangfire's defaults (UTC) when omitted.</param>
        public static void AddOrUpdateJob<TJob>(
            this IRecurringJobManager manager,
            string recurringJobId,
            Expression<Action<TJob>> methodCall,
            string cronExpression,
            RecurringJobOptions? options = null)
            where TJob : ISpiderlyJob
            => manager.AddOrUpdate(recurringJobId, methodCall, cronExpression, options ?? new RecurringJobOptions());
    }
}
