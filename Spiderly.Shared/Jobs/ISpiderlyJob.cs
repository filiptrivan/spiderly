namespace Spiderly.Shared.Jobs
{
    /// <summary>
    /// Marks a class as a Hangfire job the container builds. <c>spiderly.AddJobs(...)</c> registers every marked class
    /// in the assemblies it is given, so a host that validates its graph at boot judges the job's constructor like any
    /// other service, and the typed <c>EnqueueJob</c> / <c>AddOrUpdateJob</c> (<see cref="SpiderlyJobSchedulingExtensions"/>)
    /// accept only a marked class. The marker has no members.
    /// <example>
    /// <code>
    /// public class InvoiceExportJob : ISpiderlyJob
    /// {
    ///     private readonly IApplicationDbContext _context;
    ///
    ///     public InvoiceExportJob(IApplicationDbContext context) => _context = context;
    ///
    ///     public Task ExportAsync(long invoiceId) => ...;
    /// }
    ///
    /// backgroundJobClient.EnqueueJob&lt;InvoiceExportJob&gt;(job =&gt; job.ExportAsync(invoiceId));
    /// </code>
    /// </example>
    /// </summary>
    public interface ISpiderlyJob
    {
    }
}
