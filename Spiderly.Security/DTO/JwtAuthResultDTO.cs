using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    [SpiderlyDTO]
    public class JwtAuthResultDTO
    {
        public long UserId { get; set; }
        public AccessTokenDTO AccessTokenDTO { get; set; } = null!;
        public RefreshTokenDTO RefreshTokenDTO { get; set; } = null!;
    }
}
