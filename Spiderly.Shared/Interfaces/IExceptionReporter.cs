namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Consumer hook for reporting an unhandled exception — email, Sentry, Slack, a webhook, anything. The framework
    /// exception handler fans out to <b>all</b> registered reporters on an unhandled (500) exception in non-Development
    /// environments; each runs isolated (a throwing reporter can't break the response or starve the others). Register
    /// one or more: <c>services.AddScoped&lt;IExceptionReporter, MyReporter&gt;()</c>. The framework's
    /// <c>NotificationExceptionReporter</c> (admin email) is registered by <c>AddNotifications</c>; add a Sentry
    /// reporter alongside it, or rely on <c>Sentry.AspNetCore</c> middleware which captures exceptions pipeline-wide.
    /// </summary>
    public interface IExceptionReporter
    {
        /// <summary>
        /// Reports the exception. Must be non-blocking — it runs in the request pipeline, so offload slow work
        /// (enqueue a job, fire-and-forget an SDK call). Throwing is caught and logged by the framework.
        /// </summary>
        void Report(ExceptionReport report);
    }

    /// <summary>
    /// Context for an unhandled exception being reported. Reporters read named properties, so adding fields here
    /// (request path, method, id, …) doesn't force changes to existing <see cref="IExceptionReporter"/> implementations.
    /// </summary>
    public sealed record ExceptionReport(Exception Exception, long? UserId);
}
