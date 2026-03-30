using System.ComponentModel.DataAnnotations;

namespace Spiderly.Security.DTO
{
    public class SendLoginVerificationEmailResultDTO
    {
        [Required]
        public string Message { get; set; }
        [Required]
        public string VerificationCode { get; set; }
    }
}
