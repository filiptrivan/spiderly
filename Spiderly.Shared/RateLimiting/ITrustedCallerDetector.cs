using Microsoft.AspNetCore.Http;

namespace Spiderly.Shared.RateLimiting
{
    /// <summary>
    /// Opt-in hook that classifies a request as coming from <b>trusted first-party
    /// infrastructure</b> (the app's own SSR servers / static-site builds) rather than an
    /// arbitrary public client. When <c>AddRateLimiting()</c> is enabled and a
    /// consumer registers an implementation in DI, the global limiter routes a trusted
    /// request into its own large-budget partition
    /// (<c>Settings.TrustedRequestsLimitNumber</c>) instead of the per-IP bucket a scraper
    /// would land in — so first-party build/SSR bursts are not throttled as abuse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distinct from an authenticated API-key principal: a trusted caller carries <b>no
    /// identity and no permissions</b> — this decides a rate-limit <i>class</i>, never
    /// authorization. Keep the two axes separate: never use this to grant access.
    /// </para>
    /// <para>
    /// <b>Security.</b> A typical implementation reads a marker header injected by an edge
    /// proxy (e.g. Cloudflare) that the origin is only reachable through. That is safe
    /// ONLY while (1) the origin network admits the edge exclusively, and (2) the edge
    /// strips any client-supplied copy of the marker before forwarding. If either
    /// guarantee is removed the marker becomes client-spoofable and rate limiting can be
    /// bypassed — document the coupling at the implementation.
    /// </para>
    /// <para>Optional: no registration ⇒ no trusted class ⇒ unchanged per-IP behavior.</para>
    /// <example>
    /// <code>
    /// public sealed class EdgeMarkerTrustedCallerDetector : ITrustedCallerDetector
    /// {
    ///     public bool IsTrusted(HttpContext ctx) =&gt;
    ///         ctx.Request.Headers["X-PACMS-Trusted"] == "1";
    /// }
    /// // Startup: services.AddSingleton&lt;ITrustedCallerDetector, EdgeMarkerTrustedCallerDetector&gt;();
    /// </code>
    /// </example>
    /// </remarks>
    public interface ITrustedCallerDetector
    {
        /// <summary>
        /// Returns <c>true</c> when the request originates from trusted first-party
        /// infrastructure and should receive the trusted rate-limit partition. Must be
        /// cheap and side-effect-free (invoked in the limiter's partition selector on
        /// every request).
        /// </summary>
        /// <param name="httpContext">The current request.</param>
        bool IsTrusted(HttpContext httpContext);
    }
}
