using Spiderly.Security.Interfaces;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class LoginVerificationTokenDTO : IExpirableToken
    {
        public const string EmailIndex = nameof(Email);

        public string Email { get; set; }
        public string BrowserId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
