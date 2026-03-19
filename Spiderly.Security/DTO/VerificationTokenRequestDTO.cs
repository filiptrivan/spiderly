using Spiderly.Shared.Attributes.Entity;
using System.ComponentModel.DataAnnotations;

namespace Spiderly.Security.DTO
{
    public class VerificationTokenRequestDTO
    {
        [Required]
        [StringLength(6)]
        public string VerificationCode { get; set; }
        public string BrowserId { get; set; }
        [Required]
        [StringLength(100, MinimumLength = 5)]
        [Email]
        public string Email { get; set; }
    }
}
