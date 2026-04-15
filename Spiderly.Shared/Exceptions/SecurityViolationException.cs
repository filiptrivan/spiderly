using System;

namespace Spiderly.Shared.Exceptions
{
    /// <summary>
    /// Thrown when a request manipulates state in a way only a malicious client would (tampered
    /// tokens, forged hidden form fields, out-of-bounds file sizes, etc.). Mapped to HTTP 403
    /// with a generic user-facing message and a security-event notification in production.
    /// </summary>
    /// <example>
    /// throw new SecurityViolationException("User id mismatch between refresh and access token.");
    /// </example>
    public class SecurityViolationException : Exception
    {
        public SecurityViolationException() : base("Security violation detected.") { }

        public SecurityViolationException(string message) : base(message) { }
    }
}
