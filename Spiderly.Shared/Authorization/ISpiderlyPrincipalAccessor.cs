using System;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Transport-agnostic access to the principal executing the current logical operation. Replaces direct
    /// <c>HttpContext</c> reads so the same business code resolves identity identically under an HTTP request,
    /// a background job, or a unit test. The default implementation
    /// (<see cref="SpiderlyPrincipalAccessor"/>) is backed by an <see cref="System.Threading.AsyncLocal{T}"/>
    /// and falls back to the ambient HTTP request when nothing has been pushed explicitly.
    /// </summary>
    public interface ISpiderlyPrincipalAccessor
    {
        /// <summary>
        /// The current principal. Never <c>null</c>: returns <see cref="SpiderlyPrincipal.Anonymous"/> when no
        /// principal has been established (an unauthenticated request, or a job with no actor pushed).
        /// </summary>
        SpiderlyPrincipal Current { get; }

        /// <summary>
        /// Establishes <paramref name="principal"/> as the current principal for the calling async flow,
        /// returning a scope that restores the previous principal when disposed. Used by transport adapters
        /// (authentication middleware, background-job filters) and tests; safe to nest.
        /// </summary>
        /// <param name="principal">The principal to make current. Must not be <c>null</c>.</param>
        /// <returns>A disposable scope that restores the previous principal on dispose.</returns>
        IDisposable Push(SpiderlyPrincipal principal);
    }
}
