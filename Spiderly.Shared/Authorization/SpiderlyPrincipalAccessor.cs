using System;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Default <see cref="ISpiderlyPrincipalAccessor"/>: an <see cref="AsyncLocal{T}"/>-backed accessor that
    /// prefers an explicitly pushed principal (set by an authentication middleware, a background-job filter, or
    /// a test) and otherwise falls back to the ambient HTTP request's claims. Confines all <c>HttpContext</c>
    /// knowledge to this adapter, so the rest of the framework reads identity transport-agnostically.
    /// </summary>
    /// <remarks>
    /// Registered as a <b>singleton</b> — the per-flow value lives in the static <see cref="AsyncLocal{T}"/>,
    /// not in the instance, mirroring ASP.NET Core's <see cref="IHttpContextAccessor"/>.
    /// </remarks>
    public sealed class SpiderlyPrincipalAccessor : ISpiderlyPrincipalAccessor
    {
        private static readonly AsyncLocal<SpiderlyPrincipal> _current = new();
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>Creates the accessor over the ambient <see cref="IHttpContextAccessor"/> used for the HTTP fallback.</summary>
        /// <param name="httpContextAccessor">
        /// Accessor for the current HTTP context; its <c>HttpContext</c> is <c>null</c> outside a request
        /// (e.g. inside a background job), in which case <see cref="Current"/> reports
        /// <see cref="SpiderlyPrincipal.Anonymous"/> unless a principal has been pushed.
        /// </param>
        public SpiderlyPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public SpiderlyPrincipal Current => _current.Value ?? ResolveFromHttpContext() ?? SpiderlyPrincipal.Anonymous;

        /// <inheritdoc/>
        public IDisposable Push(SpiderlyPrincipal principal)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            SpiderlyPrincipal previous = _current.Value;
            _current.Value = principal;
            return new PrincipalScope(previous);
        }

        /// <summary>
        /// Builds a principal from the current HTTP request's authenticated claims, or <c>null</c> when there is
        /// no HTTP context (e.g. a background job) or the request is unauthenticated / carries no usable subject.
        /// </summary>
        private SpiderlyPrincipal ResolveFromHttpContext()
        {
            HttpContext httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return null;

            string subject = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(subject, out long userId) == false)
                return null;

            string kind = httpContext.User.FindFirst(PrincipalClaims.PrincipalKind)?.Value;
            return SpiderlyPrincipal.ForUser(userId, kind);
        }

        /// <summary>Restores the previous principal on dispose; idempotent.</summary>
        private sealed class PrincipalScope : IDisposable
        {
            private readonly SpiderlyPrincipal _previous;
            private bool _disposed;

            public PrincipalScope(SpiderlyPrincipal previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _current.Value = _previous;
            }
        }
    }
}
