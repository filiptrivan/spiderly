using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.RateLimiting
{
    /// <summary>
    /// Consumer-facing helpers for the trusted first-party caller class, so an app's own named
    /// rate-limit policies reuse the same trusted-check and partition-key format as the global
    /// limiter instead of re-deriving them.
    /// </summary>
    public static class TrustedCallerExtensions
    {
        /// <summary>
        /// Whether the request is a trusted first-party caller: resolves the optional
        /// <see cref="ITrustedCallerDetector"/> from the request services and returns its verdict
        /// (<c>false</c> when none is registered). Only meaningful once the framework has populated
        /// <see cref="HttpContext.RequestServices"/> (i.e. from middleware such as a rate-limit policy).
        /// </summary>
        public static bool IsTrustedCaller(this HttpContext httpContext) =>
            httpContext.RequestServices.GetService<ITrustedCallerDetector>()?.IsTrusted(httpContext) == true;

        /// <summary>
        /// The per-IP rate-limit partition key for a trusted caller. Defined once here so the global
        /// limiter and any consumer named policy agree on the key format.
        /// </summary>
        public static string TrustedPartitionKey(this HttpContext httpContext) =>
            $"trusted:{Helper.GetIPAddressOrUnknown(httpContext)}";
    }
}
