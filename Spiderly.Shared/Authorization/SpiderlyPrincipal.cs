namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Immutable snapshot of the principal executing the current logical operation, independent of transport
    /// (HTTP request, background job, test). Carries <b>identity only</b> — never permissions; authorization
    /// decisions are resolved separately through <see cref="IPrincipalRegistry"/> when an operation needs them.
    /// Obtained from <see cref="ISpiderlyPrincipalAccessor"/>.
    /// </summary>
    /// <remarks>
    /// A principal is not necessarily a person, which is why its id and <see cref="Kind"/> always travel
    /// together. Code that needs a <b>human</b> user goes through <see cref="PrincipalIdentity"/> rather than
    /// reading <see cref="PrincipalId"/> directly: ids of different kinds come from independent sequences, so a
    /// machine's id used as a user id silently resolves onto an unrelated account.
    /// </remarks>
    public sealed class SpiderlyPrincipal
    {
        /// <summary>
        /// The principal's id (subject) <b>within its own kind</b> — a user id for a human kind, a key or
        /// service-account id for a machine kind — or <c>null</c> for an anonymous or system context.
        /// </summary>
        public long? PrincipalId { get; }

        /// <summary>
        /// The principal kind (e.g. <c>User</c>, <c>ApiKey</c>, <c>ServiceAccount</c>), or <c>null</c> for the
        /// single-principal default. Mirrors the <see cref="PrincipalClaims.PrincipalKind"/> claim; the
        /// authorization core's registry resolves a <c>null</c> kind to the single registered kind.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// True when this is an authenticated caller of <b>any</b> kind — a signed-in person, an API key, a
        /// service account — i.e. it carries a <see cref="PrincipalId"/>. It does <b>not</b> assert that the
        /// caller is a person; ask <see cref="PrincipalIdentity.IsHuman"/> for that.
        /// </summary>
        public bool IsAuthenticated => PrincipalId.HasValue;

        /// <summary>
        /// True when this principal represents trusted background/system execution with no caller
        /// (e.g. a recurring Hangfire job). Authorization layers may treat this as a bypass.
        /// </summary>
        public bool IsSystem { get; }

        private SpiderlyPrincipal(long? principalId, string kind, bool isSystem)
        {
            PrincipalId = principalId;
            Kind = kind;
            IsSystem = isSystem;
        }

        /// <summary>
        /// The empty principal for a context with no established caller (an unauthenticated request, or a
        /// background job before any actor is pushed). <see cref="IsAuthenticated"/> and <see cref="IsSystem"/>
        /// are both <c>false</c> and <see cref="PrincipalId"/> is <c>null</c>.
        /// </summary>
        public static readonly SpiderlyPrincipal Anonymous = new(principalId: null, kind: null, isSystem: false);

        /// <summary>
        /// Trusted background/system execution with no caller (e.g. a recurring Hangfire job). Use this as the
        /// pushed principal for system-initiated work so audit attributes to "system" and authorization can
        /// treat <see cref="IsSystem"/> as a bypass.
        /// </summary>
        public static readonly SpiderlyPrincipal System = new(principalId: null, kind: null, isSystem: true);

        /// <summary>Creates an authenticated principal of any kind.</summary>
        /// <param name="principalId">The authenticated principal's id within its kind.</param>
        /// <param name="kind">The principal kind, or <c>null</c> for the single-principal default.</param>
        /// <returns>A principal with <see cref="IsAuthenticated"/> set to <c>true</c>.</returns>
        public static SpiderlyPrincipal ForPrincipal(long principalId, string kind = null) =>
            new(principalId, kind, isSystem: false);
    }
}
