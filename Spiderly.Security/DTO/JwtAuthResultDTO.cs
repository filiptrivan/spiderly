namespace Spiderly.Security.DTO
{
    public class JwtAuthResultDTO
    {
        public long UserId { get; set; }
        public AccessTokenDTO AccessTokenDTO { get; set; }
        public RefreshTokenDTO RefreshTokenDTO { get; set; }
    }
}
