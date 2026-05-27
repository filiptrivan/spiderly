namespace Spiderly.Security
{
    /// <summary>
    /// Well-known principal kind values (the value carried in the <see cref="PrincipalClaims.PrincipalKind"/>
    /// claim and used as the <c>kind</c> argument to <c>AddSpiderlyPrincipal&lt;T&gt;</c>).
    /// </summary>
    public static class PrincipalKinds
    {
        /// <summary>
        /// The built-in human principal kind. The framework's email-based login flow stamps this kind on the
        /// tokens it issues, so an application that uses that flow must register its human principal under it
        /// (the <c>spiderly init</c> template does: <c>AddSpiderlyPrincipal&lt;User&gt;(PrincipalKinds.User)</c>).
        /// </summary>
        public const string User = "User";
    }
}
