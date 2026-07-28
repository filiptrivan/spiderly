using System;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Thrown when code asks for a <b>human</b> user's identity while the current principal is a machine
    /// (an API key, a service account). It is a programming/wiring error surfaced loudly rather than a caller
    /// error: the alternative is resolving a machine's id against the user table and silently serving one
    /// account's data to a different principal.
    /// </summary>
    public sealed class PrincipalKindMismatchException : InvalidOperationException
    {
        /// <summary>Creates the exception for <paramref name="kind"/>.</summary>
        /// <param name="kind">The principal kind that was current, or <c>null</c> when unresolvable.</param>
        public PrincipalKindMismatchException(string? kind)
            : base($"The current principal kind '{kind ?? "(unresolved)"}' is not a human user, so it has no user " +
                   "id. An identity-scoped operation must not resolve a machine principal's id against the user " +
                   "table — the ids come from independent sequences and would collide onto an unrelated account. " +
                   "Use GetCurrentPrincipalId() for kind-agnostic work (authorization, auditing, rate limiting), " +
                   "or reject machine principals on this path.")
        {
            Kind = kind;
        }

        /// <summary>The principal kind that was current when the mismatch was detected, or <c>null</c> when unresolvable.</summary>
        public string? Kind { get; }
    }
}
