using System.Diagnostics;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// The single definition of "the current request's correlation id" — the W3C trace id that
    /// <see cref="Exceptions.SpiderlyExceptionHandler"/> puts in <see cref="DTO.ApiErrorDTO.TraceId"/> and
    /// <see cref="Extensions.RequestIdMiddleware"/> emits as the <c>X-Request-Id</c> header. Both consume
    /// this accessor so the header↔body agreement is structural, not a prose promise.
    /// </summary>
    public static class TraceCorrelation
    {
        /// <summary>The current W3C trace id, or null when no ambient <see cref="Activity"/> exists.</summary>
        public static string? CurrentTraceId() => Activity.Current?.TraceId.ToString();
    }
}
