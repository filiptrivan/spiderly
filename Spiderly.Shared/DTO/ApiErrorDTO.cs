using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Spiderly.Shared.DTO
{
    public class ApiErrorDTO
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Exception { get; set; }

        /// <summary>
        /// Machine-readable discriminator for clients (e.g. "invalid_token", "validation_failed",
        /// "unique_violation"). Null for generic errors that only surface a message.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ErrorCode { get; set; }

        /// <summary>
        /// Per-field validation errors keyed by property name. Populated for 422 responses
        /// produced by FluentValidation failures; null otherwise.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string[]> FieldErrors { get; set; }
    }
}
