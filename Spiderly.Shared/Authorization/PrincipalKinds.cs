namespace Spiderly.Shared.Authorization
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

        /// <summary>
        /// The built-in machine principal kind for API keys. The API-key authentication handler stamps this
        /// kind on the principal it issues, so an application that enables API keys registers its key entity
        /// under it (<c>AddSpiderlyPrincipal&lt;ApiKey&gt;(PrincipalKinds.ApiKey)</c>). An <c>ApiKey</c> is a
        /// first-class principal carrying its own roles, not an impersonation of its owning user.
        /// </summary>
        public const string ApiKey = "ApiKey";
    }
}
