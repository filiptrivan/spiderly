using Spiderly.Security.Interfaces;

namespace Spiderly.Security.DTO
{
    public class AccessTokenDTO : IExpirableToken
    {
        public string TokenString { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}