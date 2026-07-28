using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Security
{
    /// <summary>
    /// Rejects state-changing requests that are authenticated by <b>cookie</b> and do not carry the
    /// <c>X-CSRF</c> header. Applied globally by <c>UseSpiderlyCsrf()</c>; opt an endpoint out with
    /// <see cref="IgnoreCsrfAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Why middleware and not an attribute.</b> This check used to live inside <c>[AuthGuard]</c>, which
    /// made it opt-in per endpoint: an endpoint reachable with cookies but decorated with something else (or
    /// nothing) had no CSRF protection at all, silently. Every major framework puts CSRF in a global, opt-out
    /// layer instead — Django's <c>CsrfViewMiddleware</c>, Rails' <c>protect_from_forgery</c>, Laravel's
    /// <c>VerifyCsrfToken</c>, Spring's <c>http.csrf()</c> — and ASP.NET Core says so explicitly: "We recommend
    /// use of AutoValidateAntiforgeryToken broadly... It's more likely [otherwise] for a POST action method to be
    /// left unprotected by mistake." Making it a property of the transport is what removes the mistake.</para>
    ///
    /// <para><b>How the defense actually works, and what it depends on.</b> <c>X-CSRF</c> is a fixed marker, not a
    /// token. It protects because a <b>custom request header</b> cannot be set by a cross-origin form or image
    /// POST without first passing a CORS preflight — so the guarantee is really supplied by the app's CORS origin
    /// allow-list, and this middleware only forces attackers onto that path. <b>Consequence: an app whose CORS
    /// policy admits arbitrary origins with credentials makes this middleware a no-op.</b> ASP.NET already blocks
    /// the common form of that mistake (<c>AllowAnyOrigin()</c> and <c>AllowCredentials()</c> are mutually
    /// exclusive, and cookie auth needs credentials), so the remaining footgun is the explicit escape hatch
    /// <c>SetIsOriginAllowed(_ => true)</c> — deliberately allowing any origin. That cannot be detected statically
    /// (policies are frequently built inline in <c>UseCors</c>), so it is stated here rather than guarded: the
    /// gap this comment closes is that nothing on either side used to mention the other.</para>
    ///
    /// <para>.NET 11 ships an equivalent middleware in the box (<c>Sec-Fetch-Site</c>/<c>Origin</c> based, reusing
    /// the resolved CORS policy as its trust signal, and pointedly refusing to honor <c>AllowAnyOrigin</c> as
    /// trust). This implementation deliberately mirrors that contract — global, opt-out, CORS-derived trust — so
    /// that adopting the built-in one later is a deletion rather than a rewrite.</para>
    ///
    /// <para><b>What is deliberately NOT covered.</b> Bearer-token requests: a token in an <c>Authorization</c>
    /// header is not attached ambiently by the browser, so there is no cross-site forgery to prevent. Safe methods
    /// (<c>GET</c>/<c>HEAD</c>/<c>OPTIONS</c>): by HTTP contract they do not change state — an endpoint that
    /// mutates on <c>GET</c> is the bug, and no CSRF layer can fix it.</para>
    /// </remarks>
    public sealed class SpiderlyCsrfMiddleware
    {
        /// <summary>The header a cookie-authenticated state-changing request must carry.</summary>
        public const string HeaderName = "X-CSRF";

        private readonly RequestDelegate _next;
        private readonly string _accessTokenKey;

        /// <summary>Creates the middleware.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="tokenKeyOptions">Supplies the access-token cookie name that marks cookie authentication.</param>
        public SpiderlyCsrfMiddleware(RequestDelegate next, IOptions<TokenKeyOptions> tokenKeyOptions)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _accessTokenKey = (tokenKeyOptions ?? throw new ArgumentNullException(nameof(tokenKeyOptions)))
                .Value.AccessTokenKey;
        }

        /// <summary>Runs the check, then continues the pipeline.</summary>
        /// <param name="context">The current request.</param>
        /// <remarks>
        /// Deliberately not <c>async</c>: nothing runs after <c>_next</c>, so returning its task directly avoids
        /// allocating a state machine and a wrapper task on <b>every</b> request, this being global middleware.
        /// </remarks>
        public Task InvokeAsync(HttpContext context)
        {
            if (RequiresCsrfHeader(context, _accessTokenKey)
                && context.Request.Headers.ContainsKey(HeaderName) == false)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            return _next(context);
        }

        /// <summary>
        /// True when the request is state-changing, is authenticated by cookie rather than by an
        /// <c>Authorization</c> header, and its endpoint has not opted out via <see cref="IgnoreCsrfAttribute"/>.
        /// </summary>
        /// <param name="context">The request to classify.</param>
        /// <param name="accessTokenKey">The access-token cookie name.</param>
        public static bool RequiresCsrfHeader(HttpContext context, string accessTokenKey)
        {
            // Ordered cheapest-first: the vast majority of requests are safe methods, and of the rest most are
            // anonymous or bearer. Endpoint metadata is a reverse scan over the endpoint's whole metadata list,
            // so it runs last — only for the cookie-authenticated writes that would otherwise be challenged.
            if (IsSafeMethod(context.Request.Method))
                return false;

            // A bearer token is sent deliberately by the caller; only ambient cookie credentials are forgeable.
            if (Helper.HasBearerToken(context) || Helper.GetAccessTokenFromCookie(context, accessTokenKey) == null)
                return false;

            return context.GetEndpoint()?.Metadata.GetMetadata<IgnoreCsrfAttribute>() == null;
        }

        private static bool IsSafeMethod(string method) =>
            HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
    }
}
