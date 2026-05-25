using System;

namespace Spiderly.Shared.Exceptions
{
    /// <summary>
    /// Thrown when input is valid in shape but violates a domain rule (e.g. attempting to ship
    /// an order that's already shipped, applying a discount that's expired). Mapped to HTTP 400
    /// with the exception message passed verbatim to the client.
    /// </summary>
    /// <example>
    /// throw new BusinessException(_localizer["ProductAlreadyArchived"]);
    /// </example>
    public class BusinessException : Exception
    {
        /// <summary>
        /// Optional machine-readable code (see <see cref="Spiderly.Shared.Contracts.ApiErrorCodes"/>) surfaced
        /// in <see cref="Spiderly.Shared.DTO.ApiErrorDTO.ErrorCode"/> so clients can branch on the specific rule.
        /// Null for plain message-only business errors.
        /// </summary>
        public string ErrorCode { get; }

        public BusinessException() : base() { }

        public BusinessException(string message) : base(message) { }

        public BusinessException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
