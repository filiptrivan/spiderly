using Spiderly.Security.Interfaces;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class LoginVerificationTokenDTO : IExpirableToken
    {
        public const string EmailIndex = nameof(Email);

        public string Email { get; set; } = null!;
        public string? BrowserId { get; set; }

        /// <summary>When the code was issued (UTC). Drives the per-address resend cooldown.</summary>
        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }
    }
}
