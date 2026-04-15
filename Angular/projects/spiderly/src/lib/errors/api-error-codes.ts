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
} as const;

export type ApiErrorCode = (typeof ApiErrorCodes)[keyof typeof ApiErrorCodes];
