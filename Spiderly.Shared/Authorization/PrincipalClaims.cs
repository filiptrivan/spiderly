namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Custom claim types Spiderly stamps onto an authenticated principal in addition to the standard
    /// subject (id) claim.
    /// </summary>
    public static class PrincipalClaims
    {
        /// <summary>
        /// Identifies which principal kind the caller is (e.g. <c>"User"</c>, <c>"ServiceAccount"</c>).
        /// Absent in single-principal applications, where authorization falls back to the single
        /// registered principal kind.
        /// </summary>
        public const string PrincipalKind = "principal_kind";
    }
}
