using System.ComponentModel.DataAnnotations;

namespace Spiderly.Security.DTO
{
    /// <summary>
    /// Intentionally lowercase property names because of cookie JSON parsing on the frontend
    /// </summary>
    public class AuthResultWithCookiesDTO
    {
        [Required]
        public long userId { get; set; }
        [Required]
        public string email { get; set; }
        [Required]
        public DateTime accessTokenExpiresAt { get; set; }
    }
}
