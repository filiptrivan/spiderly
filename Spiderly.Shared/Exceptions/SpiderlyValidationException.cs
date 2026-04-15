using System;
using System.Collections.Generic;

namespace Spiderly.Shared.Exceptions
{
    /// <summary>
    /// Thrown when input fails domain-level validation that can't be expressed via
    /// FluentValidation rules. Mapped to HTTP 422 with per-field errors in the response body.
    /// For standard FluentValidation failures prefer <c>ValidateAndThrow()</c> — the handler
    /// converts those automatically.
    /// </summary>
    /// <example>
    /// throw new SpiderlyValidationException(new Dictionary&lt;string, string[]&gt;
    /// {
    ///     ["Sku"] = new[] { "SKU already exists." }
    /// });
    /// </example>
    public class SpiderlyValidationException : Exception
    {
        public Dictionary<string, string[]> Errors { get; }

        public SpiderlyValidationException(Dictionary<string, string[]> errors)
            : base("Validation failed.")
        {
            Errors = errors ?? new Dictionary<string, string[]>();
        }
    }
}
