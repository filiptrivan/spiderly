using System.ComponentModel.DataAnnotations;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class SendLoginVerificationEmailResultDTO
    {
        [Required]
        public string Message { get; set; }
        [Required]
        public string VerificationCode { get; set; }
    }
}
