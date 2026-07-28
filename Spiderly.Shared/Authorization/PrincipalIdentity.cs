using System;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Enforces the one identity rule that needs enforcing: a principal's id is only a USER id when its kind is
    /// human. "Who is calling" needs no rule — it is <c>SpiderlyPrincipal.PrincipalId</c> as-is. Pure: holds no
    /// request state, so the rule is testable without a transport.
    /// </summary>
    public sealed class PrincipalIdentity
    {
        private readonly IPrincipalRegistry _registry;

        /// <summary>Creates the resolver over the application's registered principal kinds.</summary>
        /// <param name="registry">Supplies each kind's <see cref="PrincipalNature"/>.</param>
        public PrincipalIdentity(IPrincipalRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>Whether <paramref name="principal"/> is a person. Fails closed on an unregistered kind.</summary>
        /// <param name="principal">The current principal.</param>
        public bool IsHuman(SpiderlyPrincipal principal) => _registry.IsHuman(principal.Kind);

        /// <summary>
        /// The human user's id behind <paramref name="principal"/>.
        /// </summary>
        /// <param name="principal">The current principal.</param>
        /// <exception cref="PrincipalKindMismatchException">
        /// The principal's kind is not human — including an unregistered kind, which fails closed.
        /// </exception>
        public long? GetUserId(SpiderlyPrincipal principal)
        {
            if (IsHuman(principal) == false)
                throw new PrincipalKindMismatchException(principal.Kind);

            return principal.PrincipalId;
        }
    }
}
