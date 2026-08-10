/**
 * Machine-readable error codes returned in `ApiErrorDTO.errorCode`.
 * Mirror of Spiderly.Shared.DTO.ApiErrorCodes — keep in sync.
 */
export const ApiErrorCodes = {
  InvalidToken: 'invalid_token',
  ValidationFailed: 'validation_failed',
  UniqueViolation: 'unique_violation',
  ForeignKeyViolation: 'foreign_key_violation',
  ConcurrencyConflict: 'concurrency_conflict',
  EmailNotVerified: 'email_not_verified',
  ExternalProviderNotConfigured: 'external_provider_not_configured',
  ExternalEmailMissing: 'external_email_missing',
  UnknownFilterField: 'unknown_filter_field',
  UnknownSortField: 'unknown_sort_field',
  InvalidMatchMode: 'invalid_match_mode',
} as const;

export type ApiErrorCode = (typeof ApiErrorCodes)[keyof typeof ApiErrorCodes];
