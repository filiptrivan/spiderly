using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class JwtAuthResultDTO
    {
        public long UserId { get; set; }
        public AccessTokenDTO AccessTokenDTO { get; set; }
        public RefreshTokenDTO RefreshTokenDTO { get; set; }
    }
}
