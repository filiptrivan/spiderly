namespace Spiderly.Shared.Contracts
{
    /// <summary>
    /// Machine-readable error codes returned in <see cref="Spiderly.Shared.DTO.ApiErrorDTO.ErrorCode"/>.
    /// Treat as a public contract — clients (Angular interceptor, storefront middleware,
    /// external API consumers) switch on these values.
    /// </summary>
    public static class ApiErrorCodes
    {
        /// <summary>
        /// The JWT bearer token is missing, malformed, or expired. Returned with HTTP 401
        /// (also surfaced in the <c>WWW-Authenticate</c> header); the client should refresh
        /// the token or re-authenticate.
        /// </summary>
        public const string InvalidToken = "invalid_token";

        /// <summary>
        /// One or more request fields failed server-side validation. Returned with HTTP 400;
        /// the per-field messages are carried in <c>ApiErrorDTO.FieldErrors</c>.
        /// </summary>
        public const string ValidationFailed = "validation_failed";

        /// <summary>
        /// A database unique constraint (or unique index) was violated — e.g. saving a
        /// duplicate value for a column that must be unique.
        /// </summary>
        public const string UniqueViolation = "unique_violation";

        /// <summary>
        /// A database foreign-key constraint was violated — e.g. referencing a row that does
        /// not exist, or deleting a row that is still referenced by dependent rows.
        /// </summary>
        public const string ForeignKeyViolation = "foreign_key_violation";

        /// <summary>
        /// An optimistic-concurrency check failed — the row was modified by someone else after
        /// it was loaded. The client should reload the latest data and retry.
        /// </summary>
        public const string ConcurrencyConflict = "concurrency_conflict";

        /// <summary>
        /// Login or auto-provisioning was blocked because the account's email address is not
        /// verified (e.g. an external provider returned an unverified email).
        /// </summary>
        public const string EmailNotVerified = "email_not_verified";

        /// <summary>
        /// An external (OAuth) login was attempted for a provider that is not configured on the
        /// server, or that provider's token exchange failed.
        /// </summary>
        public const string ExternalProviderNotConfigured = "external_provider_not_configured";
    }
}
