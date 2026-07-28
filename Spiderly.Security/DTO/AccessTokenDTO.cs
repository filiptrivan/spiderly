using Spiderly.Security.Interfaces;

using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class AccessTokenDTO : IExpirableToken
    {
        public string TokenString { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
