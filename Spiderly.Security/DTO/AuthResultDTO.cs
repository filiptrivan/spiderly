using System.ComponentModel.DataAnnotations;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class AuthResultDTO
    {
        [Required]
        public long UserId { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string AccessToken { get; set; }
        [Required]
        public DateTime AccessTokenExpiresAt { get; set; }
        [Required]
        public string RefreshToken { get; set; }
    }
}
