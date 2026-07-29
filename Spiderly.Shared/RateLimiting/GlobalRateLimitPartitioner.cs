using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.RateLimiting
{
    /// <summary>
    /// The sliding-window partition (key + budget) a request lands in under the global
    /// rate limiter.
    /// </summary>
    /// <param name="Key">Partition key — requests sharing a key share one budget.</param>
    /// <param name="PermitLimit">Permits allowed within <paramref name="WindowSeconds"/>.</param>
    /// <param name="WindowSeconds">Sliding-window length, in seconds.</param>
    public readonly record struct GlobalRateLimitPartition(string Key, int PermitLimit, int WindowSeconds);

    /// <summary>
    /// Pure decision for which global rate-limit partition a request belongs to, extracted
    /// from the limiter closure in <c>SpiderlyAddRateLimiters</c> so the branch is
    /// unit-testable in isolation. Three classes, evaluated most-privileged first.
    /// </summary>
    public static class GlobalRateLimitPartitioner
    {
        /// <summary>
        /// Resolves the partition for <paramref name="httpContext"/>:
        /// <list type="number">
        /// <item><b>Trusted first-party infra</b> (when a <see cref="ITrustedCallerDetector"/>
        /// is registered and matches) → its own large partition. Decides a rate-limit class
        /// only, never authorization.</item>
        /// <item><b>Authenticated API-key principal</b> → a per-key partition. Reads the
        /// VALIDATED principal (stamped by the authentication middleware), never the raw
        /// API-key header — so only meaningful after <c>UseAuthentication</c>. Machine callers
        /// aggregate many end users behind a few shared egress IPs, so a per-IP bucket both
        /// starves them and lets one machine consume unrelated clients' budget.</item>
        /// <item><b>Everyone else</b> → per-IP.</item>
        /// </list>
        /// </summary>
        /// <param name="httpContext">The current request.</param>
        /// <param name="settings">Rate-limit budgets.</param>
        /// <param name="trustedCallerDetector">Optional trusted-caller hook; <c>null</c> ⇒
        /// no trusted class (unchanged per-IP/api-key behavior).</param>
        public static GlobalRateLimitPartition Resolve(
            HttpContext httpContext,
            Settings settings,
            ITrustedCallerDetector? trustedCallerDetector)
        {
            if (trustedCallerDetector != null && trustedCallerDetector.IsTrusted(httpContext))
            {
                return new GlobalRateLimitPartition(
                    httpContext.TrustedPartitionKey(),
                    settings.TrustedRequestsLimitNumber,
                    settings.TrustedRequestsLimitWindow);
            }

            string? apiKeyId = Helper.GetAuthenticatedApiKeyId(httpContext);
            if (apiKeyId != null)
            {
                return new GlobalRateLimitPartition(
                    $"api-key:{apiKeyId}",
                    settings.ApiKeyRequestsLimitNumber,
                    settings.ApiKeyRequestsLimitWindow);
            }

            return new GlobalRateLimitPartition(
                Helper.GetIPAddressOrUnknown(httpContext),
                settings.RequestsLimitNumber,
                settings.RequestsLimitWindow);
        }
    }
}
