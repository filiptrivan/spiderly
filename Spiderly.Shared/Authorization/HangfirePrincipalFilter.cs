using System;
using Hangfire.Client;
using Hangfire.Server;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Hangfire client + server filter that carries the current principal into background work, so identity
    /// reads and audit inside a job see the right actor instead of an empty context.
    /// <para>
    /// <b>Capture (client side):</b> when a job is enqueued while an authenticated principal is current (e.g. an
    /// admin request that schedules work), the principal's id and kind are stamped onto the job.
    /// <b>Restore (server side):</b> for the duration of job execution the captured principal is pushed onto
    /// <see cref="ISpiderlyPrincipalAccessor"/>; jobs with no captured actor (recurring / scheduler-enqueued)
    /// run as <see cref="SpiderlyPrincipal.System"/>.
    /// </para>
    /// <para>
    /// Only the principal's id and kind travel with the job — never tokens or credentials. Background work is
    /// trusted (it is not re-authorized against the captured principal's live permissions); <b>attribution</b>,
    /// not authorization, is what flows in. Registered via <c>app.SpiderlyUseHangfirePrincipalFilter()</c>.
    /// </para>
    /// </summary>
    public sealed class HangfirePrincipalFilter : IClientFilter, IServerFilter
    {
        private const string UserIdParameter = "SpiderlyPrincipalUserId";
        private const string KindParameter = "SpiderlyPrincipalKind";
        private const string ScopeItemKey = "SpiderlyPrincipalScope";

        private readonly ISpiderlyPrincipalAccessor _principalAccessor;

        /// <summary>Creates the filter over the singleton principal accessor.</summary>
        /// <param name="principalAccessor">The accessor whose principal is captured at enqueue and pushed during execution.</param>
        public HangfirePrincipalFilter(ISpiderlyPrincipalAccessor principalAccessor)
        {
            _principalAccessor = principalAccessor;
        }

        /// <summary>Captures the enqueuing principal (id and kind) onto the job, when one is authenticated.</summary>
        /// <param name="context">The Hangfire job-creation context.</param>
        public void OnCreating(CreatingContext context)
        {
            SpiderlyPrincipal current = _principalAccessor.Current;
            if (current.IsAuthenticated == false || current.UserId.HasValue == false)
                return;

            context.SetJobParameter(UserIdParameter, current.UserId.Value);
            if (string.IsNullOrEmpty(current.Kind) == false)
                context.SetJobParameter(KindParameter, current.Kind);
        }

        /// <summary>No-op; capture happens in <see cref="OnCreating"/>.</summary>
        /// <param name="context">The Hangfire job-created context.</param>
        public void OnCreated(CreatedContext context) { }

        /// <summary>
        /// Pushes the captured principal — or <see cref="SpiderlyPrincipal.System"/> when none was captured —
        /// for the duration of the job, storing the restore scope in <see cref="PerformContext.Items"/>.
        /// </summary>
        /// <param name="context">The Hangfire job-performing context.</param>
        public void OnPerforming(PerformingContext context)
        {
            long? userId = context.GetJobParameter<long?>(UserIdParameter);
            SpiderlyPrincipal principal = userId.HasValue
                ? SpiderlyPrincipal.ForUser(userId.Value, context.GetJobParameter<string>(KindParameter))
                : SpiderlyPrincipal.System;

            context.Items[ScopeItemKey] = _principalAccessor.Push(principal);
        }

        /// <summary>Restores the previous principal by disposing the scope pushed in <see cref="OnPerforming"/>.</summary>
        /// <param name="context">The Hangfire job-performed context.</param>
        public void OnPerformed(PerformedContext context)
        {
            if (context.Items.TryGetValue(ScopeItemKey, out object scope) && scope is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
