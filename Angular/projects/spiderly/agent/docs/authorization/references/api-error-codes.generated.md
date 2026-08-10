<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# API error codes

Machine-readable error codes returned in ErrorCode. Treat as a public contract — clients (Angular interceptor, storefront middleware, external API consumers) switch on these values.

| Name | Value | Description |
| --- | --- | --- |
| `ConcurrencyConflict` | `concurrency_conflict` | An optimistic-concurrency check failed — the row was modified by someone else after it was loaded. The client should reload the latest data and retry. |
| `EmailNotVerified` | `email_not_verified` | Login or auto-provisioning was blocked because the account's email address is not verified (e.g. an external provider returned an unverified email). |
| `ExternalEmailMissing` | `external_email_missing` | An external (OAuth/OIDC) login was validated but the provider returned no email address (e.g. the user declined the email permission, or a phone-only Facebook account). Auto-provisioning needs an email to key the account on, so login is rejected with this code and the client should route the user to another sign-in method. Distinct from EmailNotVerified, which means an email was returned but not verified. |
| `ExternalProviderNotConfigured` | `external_provider_not_configured` | An external (OAuth) login was attempted for a provider that is not configured on the server, or that provider's token exchange failed. |
| `ForeignKeyViolation` | `foreign_key_violation` | A database foreign-key constraint was violated — e.g. referencing a row that does not exist, or deleting a row that is still referenced by dependent rows. |
| `InvalidMatchMode` | `invalid_match_mode` | A pagination filter rule used a match mode that is not valid for the field's type. Returned with HTTP 400. Thrown via PaginationErrors. |
| `InvalidToken` | `invalid_token` | The JWT bearer token is missing, malformed, or expired. Returned with HTTP 401 (also surfaced in the WWW-Authenticate header); the client should refresh the token or re-authenticate. |
| `UniqueViolation` | `unique_violation` | A database unique constraint (or unique index) was violated — e.g. saving a duplicate value for a column that must be unique. |
| `UnknownFilterField` | `unknown_filter_field` | A pagination request filtered on a key with no generated filter case. Returned with HTTP 400; the message names the key and lists the filterable fields. Thrown via PaginationErrors. |
| `UnknownSortField` | `unknown_sort_field` | A pagination request sorted on a field with no generated sort case (misspelled, or a non-sortable collection column). Returned with HTTP 400; the message names the field and lists the sortable fields. Thrown via PaginationErrors. |
| `ValidationFailed` | `validation_failed` | One or more request fields failed server-side validation. Returned with HTTP 400; the per-field messages are carried in ApiErrorDTO.FieldErrors. |
