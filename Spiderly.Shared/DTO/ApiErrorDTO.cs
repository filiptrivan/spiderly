using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Spiderly.Shared.DTO
{
    public class ApiErrorDTO
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Exception { get; set; }

        /// <summary>
        /// Machine-readable discriminator for clients (e.g. "invalid_token", "validation_failed",
        /// "unique_violation"). Null for generic errors that only surface a message.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Per-field validation errors keyed by property name. Populated for 422 responses
        /// produced by FluentValidation failures; null otherwise.
        /// </summary>
        // Nullable (not `= new()`): the WhenWritingNull condition must keep omitting the property
        // from the wire for non-validation errors — an empty dictionary would serialize as {}.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string[]>? FieldErrors { get; set; }

        /// <summary>
        /// The W3C trace id of the failed request — a support reference the client shows to the user.
        /// Populated only for reportable errors (<see cref="Exceptions.SpiderlyExceptionClassifier.IsExpected"/>
        /// == false), so expected 4xx conditions never look like incidents; null otherwise. The full
        /// correlation-id contract lives on <see cref="Extensions.RequestIdMiddleware"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TraceId { get; set; }
    }
}
