namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Immutable snapshot of the principal executing the current logical operation, independent of transport
    /// (HTTP request, background job, test). Carries <b>identity only</b> — never permissions; authorization
    /// decisions are resolved separately through <see cref="IPrincipalRegistry"/> when an operation needs them.
    /// Obtained from <see cref="ISpiderlyPrincipalAccessor"/>.
    /// </summary>
    public sealed class SpiderlyPrincipal
    {
        /// <summary>The principal's user id (subject), or <c>null</c> for an anonymous or system context.</summary>
        public long? UserId { get; }

        /// <summary>
        /// The principal kind (e.g. <c>User</c>, <c>ServiceAccount</c>), or <c>null</c> for the single-principal
        /// default. Mirrors the <see cref="PrincipalClaims.PrincipalKind"/> claim; the authorization core's
        /// registry resolves a <c>null</c> kind to the single registered kind.
        /// </summary>
        public string Kind { get; }

        /// <summary>True when this principal represents an authenticated end user (has a <see cref="UserId"/>).</summary>
        public bool IsAuthenticated { get; }

        /// <summary>
        /// True when this principal represents trusted background/system execution with no end user
        /// (e.g. a recurring Hangfire job). Authorization layers may treat this as a bypass.
        /// </summary>
        public bool IsSystem { get; }

        private SpiderlyPrincipal(long? userId, string kind, bool isAuthenticated, bool isSystem)
        {
            UserId = userId;
            Kind = kind;
            IsAuthenticated = isAuthenticated;
            IsSystem = isSystem;
        }

        /// <summary>
        /// The empty principal for a context with no established caller (an unauthenticated request, or a
        /// background job before any actor is pushed). <see cref="IsAuthenticated"/> and <see cref="IsSystem"/>
        /// are both <c>false</c> and <see cref="UserId"/> is <c>null</c>.
        /// </summary>
        public static readonly SpiderlyPrincipal Anonymous = new(userId: null, kind: null, isAuthenticated: false, isSystem: false);

        /// <summary>
        /// Trusted background/system execution with no end user (e.g. a recurring Hangfire job). Use this as the
        /// pushed principal for system-initiated work so audit attributes to "system" and authorization can
        /// treat <see cref="IsSystem"/> as a bypass.
        /// </summary>
        public static readonly SpiderlyPrincipal System = new(userId: null, kind: null, isAuthenticated: false, isSystem: true);

        /// <summary>Creates an authenticated end-user principal.</summary>
        /// <param name="userId">The authenticated user's id (subject).</param>
        /// <param name="kind">The principal kind, or <c>null</c> for the single-principal default.</param>
        /// <returns>A principal with <see cref="IsAuthenticated"/> set to <c>true</c>.</returns>
        public static SpiderlyPrincipal ForUser(long userId, string kind = null) =>
            new(userId, kind, isAuthenticated: true, isSystem: false);
    }
}
