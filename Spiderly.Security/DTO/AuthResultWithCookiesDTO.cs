using System.ComponentModel.DataAnnotations;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    /// <summary>
    /// Intentionally lowercase property names because of cookie JSON parsing on the frontend
    /// </summary>
    [SpiderlyDTO]
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
