using System.ComponentModel.DataAnnotations;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class SendLoginVerificationEmailResultDTO
    {
        [Required]
        public string Message { get; set; } = null!;
        /// <summary>Only populated in the development inline-code mode (no emailing configured); null in production.</summary>
        [Required]
        public string? VerificationCode { get; set; }
    }
}
