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
        public BusinessException() : base() { }

        public BusinessException(string message) : base(message) { }
    }
}
