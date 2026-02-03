using Spiderly.Security.Interfaces;

namespace Spiderly.Security.DTO
{
    public class LoginVerificationTokenDTO : IExpirableToken
    {
        public string Email { get; set; }
        public string BrowserId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
