using Microsoft.AspNetCore.Http;
using Spiderly.Shared.RateLimiting;

namespace Spiderly.Shared.Tests
{
    // Shared test double: returns a fixed trusted/untrusted verdict so a test can drive either
    // branch without an edge-injected marker. Used by the partitioner unit tests and the
    // rate-limiter integration test.
    internal sealed class StubTrustedCallerDetector : ITrustedCallerDetector
    {
        private readonly bool _trusted;
        public StubTrustedCallerDetector(bool trusted) => _trusted = trusted;
        public bool IsTrusted(HttpContext httpContext) => _trusted;
    }
}
