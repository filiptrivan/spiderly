namespace Spiderly.Shared.Contracts
{
    /// <summary>
    /// Machine-readable error codes returned in <see cref="Spiderly.Shared.DTO.ApiErrorDTO.ErrorCode"/>.
    /// Treat as a public contract — clients (Angular interceptor, storefront middleware,
    /// external API consumers) switch on these values.
    /// </summary>
    public static class ApiErrorCodes
    {
        public const string InvalidToken = "invalid_token";
        public const string ValidationFailed = "validation_failed";
        public const string UniqueViolation = "unique_violation";
        public const string ForeignKeyViolation = "foreign_key_violation";
        public const string ConcurrencyConflict = "concurrency_conflict";
    }
}
