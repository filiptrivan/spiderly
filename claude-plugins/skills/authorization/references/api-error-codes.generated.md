<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# API error codes

Machine-readable error codes returned in ErrorCode. Treat as a public contract — clients (Angular interceptor, storefront middleware, external API consumers) switch on these values.

| Name | Value | Description |
| --- | --- | --- |
| `ConcurrencyConflict` | `concurrency_conflict` | An optimistic-concurrency check failed — the row was modified by someone else after it was loaded. The client should reload the latest data and retry. |
| `EmailNotVerified` | `email_not_verified` | Login or auto-provisioning was blocked because the account's email address is not verified (e.g. an external provider returned an unverified email). |
| `ExternalProviderNotConfigured` | `external_provider_not_configured` | An external (OAuth) login was attempted for a provider that is not configured on the server, or that provider's token exchange failed. |
| `ForeignKeyViolation` | `foreign_key_violation` | A database foreign-key constraint was violated — e.g. referencing a row that does not exist, or deleting a row that is still referenced by dependent rows. |
| `InvalidToken` | `invalid_token` | The JWT bearer token is missing, malformed, or expired. Returned with HTTP 401 (also surfaced in the WWW-Authenticate header); the client should refresh the token or re-authenticate. |
| `UniqueViolation` | `unique_violation` | A database unique constraint (or unique index) was violated — e.g. saving a duplicate value for a column that must be unique. |
| `ValidationFailed` | `validation_failed` | One or more request fields failed server-side validation. Returned with HTTP 400; the per-field messages are carried in ApiErrorDTO.FieldErrors. |
